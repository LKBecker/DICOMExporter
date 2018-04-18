using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;

namespace DICOMExporter {
	public enum DICOMFileType { NotDicom, Dicom3File, DicomOldTypeFile, DicomUnknownTransferSyntax };

	internal class DicomFile {
		#region Constants
		private const uint PIXEL_REPRESENTATION = 0x00280103,
			TRANSFER_SYNTAX_UID = 0x00020010,
			MODALITY = 0x00080060,
			SLICE_THICKNESS = 0x00180050,
			SLICE_SPACING = 0x00180088,
			SAMPLES_PER_PIXEL = 0x00280002,
			PHOTOMETRIC_INTERPRETATION = 0x00280004,
			PLANAR_CONFIGURATION = 0x00280006,
			NUMBER_OF_FRAMES = 0x00280008,
			ROWS = 0x00280010,
			COLUMNS = 0x00280011,
			PIXEL_SPACING = 0x00280030,
			BITS_ALLOCATED = 0x00280100,
			WINDOW_CENTER = 0x00281050,
			WINDOW_WIDTH = 0x00281051,
			RESCALE_INTERCEPT = 0x00281052,
			RESCALE_SLOPE = 0x00281053,
			RED_PALETTE = 0x00281201,
			GREEN_PALETTE = 0x00281202,
			BLUE_PALETTE = 0x00281203,
			ICON_IMAGE_SEQUENCE = 0x00880200,
			PIXEL_DATA = 0x7FE00010;
 
		private const string ITEM = "FFFEE000",
			ITEM_DELIMITATION = "FFFEE00D",
			SEQUENCE_DELIMITATION = "FFFEE0DD",
			DICM = "DICM";

		private const int
			AE = 0x4145,
			AS = 0x4153,
			AT = 0x4154,
			CS = 0x4353,
			DA = 0x4441,
			DS = 0x4453,
			DT = 0x4454,
			FD = 0x4644,
			FL = 0x464C,
			IS = 0x4953,
			LO = 0x4C4F,
			LT = 0x4C54,
			PN = 0x504E,
			SH = 0x5348,
			SL = 0x534C,
			SS = 0x5353,
			ST = 0x5354,
			TM = 0x544D,
			UI = 0x5549,
			UL = 0x554C,
			US = 0x5553,
			UT = 0x5554,
			OB = 0x4F42,
			OW = 0x4F57,
			SQ = 0x5351,
			UN = 0x554E,
			QQ = 0x3F3F,
			RT = 0x5254,
			ID_OFFSET = 128,  //location of "DICM"
			IMPLICIT_VR = 0x2D2D; // '--' 
		#endregion
		#region Fields
		public int bitsAllocated;
		public int width;
		public int height;
		public int offset;
		public int nImages;
		public int samplesPerPixel;
		public double pixelDepth = 1.0;
		public double pixelWidth = 1.0;
		public double pixelHeight = 1.0;
		public string unit;
		public double windowCentre, windowWidth;
		public bool signedImage;
		public DICOMFileType typeofDicomFile;
		public List<string> dicomInfo;
		public bool dicmFound; // "DICM" found at offset 128

		private BinaryReader file;
		private string dicomFileName;
		private string photoInterpretation;
		private bool littleEndian = true;
		private bool oddLocations;  // one or more tags at odd locations
		private bool bigEndianTransferSyntax = false;
		private bool inSequence;
		private bool widthTagFound;
		private bool heightTagFound;
		private bool pixelDataTagFound;
		private int currentStreamLocation = 0;
		private int elementLength;
		private int vr;  // Value Representation
		private int pixelRepresentation;
		private double rescaleIntercept;
		private double rescaleSlope;
		private byte[] reds;
		private byte[] greens;
		private byte[] blues;
		private byte[] vrLetters = new byte[2];
		private List<byte> pixels8;
		private List<byte> pixels24; // 8 bits bit depth, 3 samples per pixel
		private List<ushort> pixels16;
		private List<int> pixels16Int;
		#endregion

		public DicomFile() {
			signedImage = false;
			dicomInfo = new List<string>();
			InitializeDicom();
		}

		/// <summary> Return all properties to default values </summary>
		void InitializeDicom() {
			bitsAllocated = 0;
			width = 1;
			height = 1;
			offset = 1;
			nImages = 1;
			samplesPerPixel = 1;
			photoInterpretation = "";
			unit = "mm";
			windowCentre = 0;
			windowWidth = 0;
			signedImage = false;
			widthTagFound = false;
			heightTagFound = false;
			pixelDataTagFound = false;
			rescaleIntercept = 0.0; // Default value
			rescaleSlope = 1.0; // Default value
			typeofDicomFile = DICOMFileType.NotDicom;
		}

		/// <summary>
		/// Forces reinitialisation of the object on change, including reading of metadata.
		/// </summary>
		public string DicomFileName {
			set {
				dicomFileName = value;
				InitializeDicom();

				// Thanks to CodeProject member Alphons van der Heijden for 
				//   suggesting to add this - FileAccess.Read  (July 2010)
				file = new BinaryReader(File.Open(dicomFileName, FileMode.Open, FileAccess.Read));
				//currentStreamLocation = 0; // Reset the location
				dicomInfo.Clear();
				try {
					bool readResult = ReadFileInfo();
					if (readResult && widthTagFound && heightTagFound && pixelDataTagFound) {
						ReadPixels();
						if (dicmFound == true) { typeofDicomFile = DICOMFileType.Dicom3File; }
						else { typeofDicomFile = DICOMFileType.DicomOldTypeFile; }
					}
				}
				catch { }
				finally { file.Close(); }
			}
		}

		public void GetPixels8(ref List<byte> pixels) { pixels = pixels8; }
		public void GetPixels16(ref List<ushort> pixels) { pixels = pixels16; }
		public void GetPixels24(ref List<byte> pixels) { pixels = pixels24; }

		/// <summary>
		/// Extracts bytes from the DICOM filestream and converts them to their ASCII representation
		/// </summary>
		/// <param name="length">The number of bytes to extract</param>
		/// <returns></returns>
		string GetString(int length) {
			if (length <= 0) { throw new ArgumentException("Number of bytes to extract must be 1 or greater"); }
			byte[] buf = new byte[length];
			//file.BaseStream.Position = currentStreamLocation;
			int count = file.Read(buf, 0, length);
			//currentStreamLocation += length;
			string s = System.Text.ASCIIEncoding.ASCII.GetString(buf);
			return s;
		}

		byte GetByte(){
			//file.BaseStream.Position = this.currentStreamLocation;
			//byte b = file.ReadByte(); //advances position automatically, but we use currentStreamLocation so...
			//++currentStreamLocation;
			//return b;
			return file.ReadByte();
		}

		ushort GetUnsignedShort(){
			byte b0 = GetByte();
			byte b1 = GetByte();
			ushort s;
			if (littleEndian) { s = Convert.ToUInt16((b1 << 8) + b0); }
			else { s = Convert.ToUInt16((b0 << 8) + b1); }
			return s;
		}

		int GetInt() {
			byte b0 = GetByte();
			byte b1 = GetByte();
			byte b2 = GetByte();
			byte b3 = GetByte();
			int i;
			if (littleEndian) { i = (b3 << 24) + (b2 << 16) + (b1 << 8) + b0; }
			else { i = (b0 << 24) + (b1 << 16) + (b2 << 8) + b3; }
			return i;
		}

		double GetDouble() {
			byte b0 = GetByte();
			byte b1 = GetByte();
			byte b2 = GetByte();
			byte b3 = GetByte();
			byte b4 = GetByte();
			byte b5 = GetByte();
			byte b6 = GetByte();
			byte b7 = GetByte();

			long res = 0;
			if (littleEndian) {
				res += b0;
				res += (((long)b1) << 8);
				res += (((long)b2) << 16);
				res += (((long)b3) << 24);
				res += (((long)b4) << 32);
				res += (((long)b5) << 40);
				res += (((long)b6) << 48);
				res += (((long)b7) << 56);
			}
			else {
				res += b7;
				res += (((long)b6) << 8);
				res += (((long)b5) << 16);
				res += (((long)b4) << 24);
				res += (((long)b3) << 32);
				res += (((long)b2) << 40);
				res += (((long)b1) << 48);
				res += (((long)b0) << 56);
			}

			double d = Convert.ToDouble(res, new CultureInfo("en-US"));
			return d;
		}

		float GetFloat() {
			byte b0 = GetByte();
			byte b1 = GetByte();
			byte b2 = GetByte();
			byte b3 = GetByte();

			int res = 0;

			if (littleEndian) {
				res += b0;
				res += (((int)b1) << 8);
				res += (((int)b2) << 16);
				res += (((int)b3) << 24);
			}
			else {
				res += b3;
				res += (((int)b2) << 8);
				res += (((int)b1) << 16);
				res += (((int)b0) << 24);
			}

			float f1;
			f1 = Convert.ToSingle(res, new CultureInfo("en-US"));
			return f1;
		}

		byte[] GetLut(int length) {
			if ((length & 1) != 0){ // odd
				string dummy = GetString(length); //advances string position and not much else?
				return null;
			}

			length /= 2;
			byte[] lut = new byte[length];
			for (int i = 0; i < length; ++i) { lut[i] = Convert.ToByte(GetUnsignedShort() >> 8); }
			return lut;
		}

		int GetLength() {
			byte b0 = GetByte();
			byte b1 = GetByte();
			byte b2 = GetByte();
			byte b3 = GetByte();

			// Cannot know whether the VR is implicit or explicit without the 
			// complete Dicom Data Dictionary. 
			vr = (b0 << 8) + b1;

			switch (vr) {
				case OB:
				case OW:
				case SQ:
				case UN:
				case UT:
					// Explicit VR with 32-bit length if other two bytes are zero
					if ((b2 == 0) || (b3 == 0)) return GetInt();
					// Implicit VR with 32-bit length
					vr = IMPLICIT_VR;
					if (littleEndian)
						return ((b3 << 24) + (b2 << 16) + (b1 << 8) + b0);
					else
						return ((b0 << 24) + (b1 << 16) + (b2 << 8) + b3);
				// break; // Not necessary
				case AE:
				case AS:
				case AT:
				case CS:
				case DA:
				case DS:
				case DT:
				case FD:
				case FL:
				case IS:
				case LO:
				case LT:
				case PN:
				case SH:
				case SL:
				case SS:
				case ST:
				case TM:
				case UI:
				case UL:
				case US:
				case QQ:
				case RT:
					// Explicit vr with 16-bit length
					if (littleEndian)
						return ((b3 << 8) + b2);
					else
						return ((b2 << 8) + b3);
				default:
					// Implicit VR with 32-bit length...
					vr = IMPLICIT_VR;
					if (littleEndian)
						return ((b3 << 24) + (b2 << 16) + (b1 << 8) + b0);
					else
						return ((b0 << 24) + (b1 << 16) + (b2 << 8) + b3);
			}
		}

		void GetSpatialScale(string scale) {
			double xscale = 0, yscale = 0;
			int i = scale.IndexOf('\\');
			if (i == 1) // Aug 2012, Fixed an issue found while opening some images
            {
				yscale = Convert.ToDouble(scale.Substring(0, i), new CultureInfo("en-US"));
				xscale = Convert.ToDouble(scale.Substring(i + 1), new CultureInfo("en-US"));
			}
			if (xscale != 0.0 && yscale != 0.0) {
				pixelWidth = xscale;
				pixelHeight = yscale;
				unit = "mm";
			}
		}

		int GetNextTag() {
			int groupWord = GetUnsignedShort();
			if (groupWord == 0x0800 && bigEndianTransferSyntax) {
				littleEndian = false;
				groupWord = 0x0008;
			}
			int elementWord = GetUnsignedShort();
			int tag = groupWord << 16 | elementWord;

			elementLength = GetLength();

			// HACK to read some GE files
			if (elementLength == 13 && !oddLocations)
				elementLength = 10;

			// "Undefined" element length.
			// This is a sort of bracket that encloses a sequence of elements.
			if (elementLength == -1) {
				elementLength = 0;
				inSequence = true;
			}
			return tag;
		}

		string GetHeaderInfo(int tag, string value) {
			string str = tag.ToString("X8"); //conversion of int tag to a hexadecimal string representation of its value
			if (str == ITEM_DELIMITATION || str == SEQUENCE_DELIMITATION) {
				inSequence = false;
				return null;
			}

			string id = null;

			if (DICOMDictionary.ContainsKey(str)) {
				id = DICOMDictionary[str];
				if (id != null) {
					if (vr == IMPLICIT_VR) { vr = (id[0] << 8) + id[1]; }
					id = id.Substring(2); //cuts off initial two letters, which are those argument codes
				}
			}

			if (str == ITEM) { return (id != null ? id : ":null"); }
			if (value != null) { return id + ": " + value; }

			switch (vr) {
				case FD:
					for (int i = 0; i < elementLength; ++i)
						GetByte();
					break;
				case FL:
					for (int i = 0; i < elementLength; i++)
						GetByte();
					break;
				case AE:
				case AS:
				case AT:
				case CS:
				case DA:
				case DS:
				case DT:
				case IS:
				case LO:
				case LT:
				case PN:
				case SH:
				case ST:
				case TM:
				case UI:
					value = GetString(elementLength);
					break;
				case US:
					if (elementLength == 2) { value = Convert.ToString(GetUnsignedShort()); }
					else {
						value = "";
						int n = elementLength / 2;
						for (int i = 0; i < n; i++)
							value += Convert.ToString(GetUnsignedShort()) + " ";
					}
					break;
				case IMPLICIT_VR:
					value = GetString(elementLength);
					if (elementLength > 44) { value = null; }
					break;
				case SQ:
					value = "";
					bool privateTag = ((tag >> 16) & 1) != 0;
					if (tag != ICON_IMAGE_SEQUENCE && !privateTag) { break; }
					goto default;
				default:
					currentStreamLocation += elementLength;
					value = "";
					break;
			}

			if (value != null && id == null && value != "") { return "---: " + value; }
			else if (id == null) { return null; }
			else { return id + ": " + value; }
		}

		void AddInfo(int tag, string value) {
			string info = GetHeaderInfo(tag, value);

			string str = tag.ToString("X");
			string strPadded = str.PadLeft(8, '0');
			string strInfo;
			if (inSequence && info != null && vr != SQ) { info = ">" + info; }
			if (info != null && str != ITEM) {
				if (info.Contains("---")) { strInfo = info.Replace("---", "Private Tag"); }
				else { strInfo = info; }
				dicomInfo.Add(strPadded + "//" + strInfo);
			}
		}

		void AddInfo(int tag, int value) {
			AddInfo(tag, Convert.ToString(value));
		}

		/// <summary> Extracts File Type and embedded metadata tags from the file </summary>
		/// <returns>A bool indicating successful extraction</returns>
		public bool ReadFileInfo() {
			bitsAllocated = 16;

			file.BaseStream.Seek(Convert.ToInt32(ID_OFFSET), SeekOrigin.Begin);
			//currentStreamLocation += ID_OFFSET;

			if (GetString(4) != DICM) { // This is for reading older DICOM files (Before 3.0). Seek from the beginning of the file
				file.BaseStream.Seek(0, SeekOrigin.Begin);
				//currentStreamLocation = 0;
				dicmFound = false; // Older DICOM files do not have the preamble and prefix
				// Continue reading further. See whether the width, height and pixel data tags are present. 
				// If these tags are present, then it we conclude that this is a DICOM file, because all DICOM files should have at least these three tags.
			}
			else { dicmFound = true; } // We have a DICOM 3.0 file

			bool decodingTags = true;
			samplesPerPixel = 1;
			int planarConfiguration = 0;
			photoInterpretation = "";
			string modality;

			while (decodingTags) {
				int tag = GetNextTag();
				//if ((currentStreamLocation & 1) != 0) { oddLocations = true; }
				if ((file.BaseStream.Position & 1) != 0) { oddLocations = true; }

				if (inSequence) {
					AddInfo(tag, null);
					continue;
				}

				string s;
				switch (tag) {
					case (int)(TRANSFER_SYNTAX_UID):
						s = GetString(elementLength);
						AddInfo(tag, s);
						if (s.IndexOf("1.2.4") > -1 || s.IndexOf("1.2.5") > -1) {
							file.Close();
							typeofDicomFile = DICOMFileType.DicomUnknownTransferSyntax;
							// Return gracefully indicating that this type of Transfer Syntax cannot be handled
							return false;
						}
						if (s.IndexOf("1.2.840.10008.1.2.2") >= 0) { bigEndianTransferSyntax = true; }
						break;

					case (int)MODALITY:
						modality = GetString(elementLength);
						AddInfo(tag, modality);
						break;

					case (int)(NUMBER_OF_FRAMES):
						s = GetString(elementLength);
						AddInfo(tag, s);
						double frames = Convert.ToDouble(s, new CultureInfo("en-US"));
						if (frames > 1.0) { nImages = (int)frames; }
						break;

					case (int)(SAMPLES_PER_PIXEL):
						samplesPerPixel = GetUnsignedShort();
						AddInfo(tag, samplesPerPixel);
						break;

					case (int)(PHOTOMETRIC_INTERPRETATION):
						photoInterpretation = GetString(elementLength);
						photoInterpretation = photoInterpretation.Trim();
						AddInfo(tag, photoInterpretation);
						break;

					case (int)(PLANAR_CONFIGURATION):
						planarConfiguration = GetUnsignedShort();
						AddInfo(tag, planarConfiguration);
						break;

					case (int)(ROWS):
						height = GetUnsignedShort();
						AddInfo(tag, height);
						heightTagFound = true;
						break;

					case (int)(COLUMNS):
						width = GetUnsignedShort();
						AddInfo(tag, width);
						widthTagFound = true;
						break;

					case (int)(PIXEL_SPACING):
						string scale = GetString(elementLength);
						GetSpatialScale(scale);
						AddInfo(tag, scale);
						break;

					case (int)(SLICE_THICKNESS):
					case (int)(SLICE_SPACING):
						string spacing = GetString(elementLength);
						pixelDepth = Convert.ToDouble(spacing, new CultureInfo("en-US"));
						AddInfo(tag, spacing);
						break;

					case (int)(BITS_ALLOCATED):
						bitsAllocated = GetUnsignedShort();
						AddInfo(tag, bitsAllocated);
						break;

					case (int)(PIXEL_REPRESENTATION):
						pixelRepresentation = GetUnsignedShort();
						AddInfo(tag, pixelRepresentation);
						break;

					case (int)(WINDOW_CENTER):
						string center = GetString(elementLength);
						int index = center.IndexOf('\\');
						if (index != -1) center = center.Substring(index + 1);
						windowCentre = Convert.ToDouble(center, new CultureInfo("en-US"));
						AddInfo(tag, center);
						break;

					case (int)(WINDOW_WIDTH):
						string widthS = GetString(elementLength);
						index = widthS.IndexOf('\\');
						if (index != -1) widthS = widthS.Substring(index + 1);
						windowWidth = Convert.ToDouble(widthS, new CultureInfo("en-US"));
						AddInfo(tag, widthS);
						break;

					case (int)(RESCALE_INTERCEPT):
						string intercept = GetString(elementLength);
						rescaleIntercept = Convert.ToDouble(intercept, new CultureInfo("en-US"));
						AddInfo(tag, intercept);
						break;

					case (int)(RESCALE_SLOPE):
						string slop = GetString(elementLength);
						rescaleSlope = Convert.ToDouble(slop, new CultureInfo("en-US"));
						AddInfo(tag, slop);
						break;

					case (int)(RED_PALETTE):
						reds = GetLut(elementLength);
						AddInfo(tag, elementLength / 2);
						break;

					case (int)(GREEN_PALETTE):
						greens = GetLut(elementLength);
						AddInfo(tag, elementLength / 2);
						break;

					case (int)(BLUE_PALETTE):
						blues = GetLut(elementLength);
						AddInfo(tag, elementLength / 2);
						break;

					case (int)(PIXEL_DATA):
						// Start of image data...
						if (elementLength != 0) {
							//offset = currentStreamLocation;
							offset = (int)file.BaseStream.Position;
							AddInfo(tag, offset);
							decodingTags = false;
						}
						else { AddInfo(tag, null); }
						pixelDataTagFound = true;
						break;

					default:
						AddInfo(tag, null);
						break;
				}
			}
			return true;
		}

		void ReadPixels() {
			if (samplesPerPixel == 1 && bitsAllocated == 8) {
				if (pixels8 != null) { pixels8.Clear(); }
				pixels8 = new List<byte>();
				int numPixels = width * height;
				byte[] buf = new byte[numPixels];
				file.BaseStream.Position = offset;
				file.Read(buf, 0, numPixels);

				for (int i = 0; i < numPixels; ++i) {
					int pixVal = (int)(buf[i] * rescaleSlope + rescaleIntercept);
					// We internally convert all 8-bit images to the range 0 - 255
					//if (photoInterpretation.Equals("MONOCHROME1", stringComparison.OrdinalIgnoreCase))
					//    pixVal = 65535 - pixVal;
					if (photoInterpretation == "MONOCHROME1") { pixVal = Byte.MaxValue - pixVal; }
					pixels8.Add((byte)(pixelRepresentation == 1 ? pixVal : (pixVal - Byte.MinValue)));
				}
			}

			if (samplesPerPixel == 1 && bitsAllocated == 16) {
				if (pixels16 != null) { pixels16.Clear(); }
				if (pixels16Int != null) { pixels16Int.Clear(); }

				pixels16 = new List<ushort>();
				pixels16Int = new List<int>();
				int numPixels = width * height;
				byte[] bufByte = new byte[numPixels * 2];
				byte[] signedData = new byte[2];
				file.BaseStream.Position = offset;
				file.Read(bufByte, 0, numPixels * 2);
				ushort unsignedS;
				int i, i1, pixVal;
				byte b0, b1;

				for (i = 0; i < numPixels; ++i) {
					i1 = i * 2;
					b0 = bufByte[i1];
					b1 = bufByte[i1 + 1];
					unsignedS = Convert.ToUInt16((b1 << 8) + b0);
					if (pixelRepresentation == 0) { // Unsigned
						pixVal = (int)(unsignedS * rescaleSlope + rescaleIntercept);
						if (photoInterpretation == "MONOCHROME1") { pixVal = ushort.MaxValue - pixVal; }
					}
					else { // Pixel representation is 1, indicating a 2s complement image
						signedData[0] = b0;
						signedData[1] = b1;
						short sVal = System.BitConverter.ToInt16(signedData, 0);

						// Need to consider rescale slope and intercepts to compute the final pixel value
						pixVal = (int)(sVal * rescaleSlope + rescaleIntercept);
						if (photoInterpretation == "MONOCHROME1") { pixVal = ushort.MaxValue - pixVal; }
					}
					pixels16Int.Add(pixVal);
				}

				int minPixVal = pixels16Int.Min();
				signedImage = false;
				if (minPixVal < 0) signedImage = true;

				// Use the above pixel data to populate the list pixels16 
				foreach (int pixel in pixels16Int) {
					// We internally convert all 16-bit images to the range 0 - 65535
					if (signedImage) { pixels16.Add((ushort)(pixel - short.MinValue)); } //TODO short or ushort??
					else { pixels16.Add((ushort)(pixel)); }
				}
				pixels16Int.Clear();
			}

			if (samplesPerPixel == 3 && bitsAllocated == 8) {
				signedImage = false;
				if (pixels24 != null) { pixels24.Clear(); }
				pixels24 = new List<byte>();
				int numPixels = width * height;
				int numBytes = numPixels * samplesPerPixel;
				byte[] buf = new byte[numBytes];
				file.BaseStream.Position = offset;
				file.Read(buf, 0, numBytes);

				for (int i = 0; i < numBytes; ++i) { pixels24.Add(buf[i]); }
			}
		}

#region DICOMDictionary
		// Dicom Dictionary. Heavily adapted from code by Amarnath S, Mahesh Reddy S
		// This Dicom Dictionary does not contain all tags specified in Part 6 of the Standard, but contains tags used 80 percent of the time.
		public static Dictionary<string, string> DICOMDictionary = new Dictionary<string,string>(){
            {"00020002", "UIMedia Storage SOP Class UID"}, 
            {"00020003", "UIMedia Storage SOP Instance UID"},
            {"00020010", "UITransfer Syntax UID"},
            {"00020012", "UIImplementation Class UID"},
            {"00020013", "SHImplementation Version Name"},
            {"00020016", "AESource Application Entity Title"}, 
		
            {"00080005", "CSSpecific Character Set"},
            {"00080008", "CSImage Type"},
            {"00080010", "CSRecognition Code"},
            {"00080012", "DAInstance Creation Date"},
            {"00080013", "TMInstance Creation Time"},
            {"00080014", "UIInstance Creator UID"},
            {"00080016", "UISOP Class UID"},
            {"00080018", "UISOP Instance UID"},
            {"00080020", "DAStudy Date"},
            {"00080021", "DASeries Date"},
            {"00080022", "DAAcquisition Date"},
            {"00080023", "DAContent Date"},
            {"00080024", "DAOverlay Date"},
            {"00080025", "DACurve Date"},
            {"00080030", "TMStudy Time"},
            {"00080031", "TMSeries Time"},
            {"00080032", "TMAcquisition Time"},
            {"00080033", "TMContent Time"},
            {"00080034", "TMOverlay Time"},
            {"00080035", "TMCurve Time"},
            {"00080040", "USData Set Type"},
            {"00080041", "LOData Set Subtype"},
            {"00080042", "CSNuclear Medicine Series Type"},
            {"00080050", "SHAccession Number"},
            {"00080052", "CSQuery/Retrieve Level"},
            {"00080054", "AERetrieve AE Title"},
            {"00080058", "AEFailed SOP Instance UID List"},
            {"00080060", "CSModality"},
            {"00080064", "CSConversion Type"},
            {"00080068", "CSPresentation Intent Type"},
            {"00080070", "LOManufacturer"},
            {"00080080", "LOInstitution Name"},
            {"00080081", "STInstitution Address"},
            {"00080082", "SQInstitution Code Sequence"},
            {"00080090", "PNReferring Physician's Name"},
            {"00080092", "STReferring Physician's Address"},
            {"00080094", "SHReferring Physician's Telephone Numbers"},
            {"00080096", "SQReferring Physician Identification Sequence"},
            {"00080100", "SHCode Value"},
            {"00080102", "SHCoding Scheme Designator"},
            {"00080103", "SHCoding Scheme Version"},
            {"00080104", "LOCode Meaning"},
            {"00080201", "SHTimezone Offset From UTC"},
            {"00081010", "SHStation Name"},
            {"00081030", "LOStudy Description"},
            {"00081032", "SQProcedure Code Sequence"},
            {"0008103E", "LOSeries Description"},
            {"00081040", "LOInstitutional Department Name"},
            {"00081048", "PNPhysician(s) of Record"},
            {"00081050", "PNPerforming Physician's Name"},
            {"00081060", "PNName of Physician(s) Reading Study"},
            {"00081070", "PNOperator's Name"},
            {"00081080", "LOAdmitting Diagnoses Description"},
            {"00081084", "SQAdmitting Diagnoses Code Sequence"},
            {"00081090", "LOManufacturer's Model Name"},
            {"00081100", "SQReferenced Results Sequence"},
            {"00081110", "SQReferenced Study Sequence"},
            {"00081111", "SQReferenced Performed Procedure Step Sequence"},
            {"00081115", "SQReferenced Series Sequence"},
            {"00081120", "SQReferenced Patient Sequence"},
            {"00081125", "SQReferenced Visit Sequence"},
            {"00081130", "SQReferenced Overlay Sequence"},
            {"00081140", "SQReferenced Image Sequence"},
            {"00081145", "SQReferenced Curve Sequence"},
            {"00081150", "UIReferenced SOP Class UID"},
            {"00081155", "UIReferenced SOP Instance UID"},
            {"00082111", "STDerivation Description"},
            {"00082112", "SQSource Image Sequence"},
            {"00082120", "SHStage Name"},
            {"00082122", "ISStage Number"},
            {"00082124", "ISNumber of Stages"},
            {"00082129", "ISNumber of Event Timers"},
            {"00082128", "ISView Number"},
            {"0008212A", "ISNumber of Views in Stage"},
            {"00082130", "DSEvent Elapsed Time(s)"},
            {"00082132", "LOEvent Timer Name(s)"},
            {"00082142", "ISStart Trim"},
            {"00082143", "ISStop Trim"},
            {"00082144", "ISRecommended Display Frame Rate"},
            {"00082200", "CSTransducer Position"},
            {"00082204", "CSTransducer Orientation"},
            {"00082208", "CSAnatomic Structure"},

            {"00100010", "PNPatient's Name"},
            {"00100020", "LOPatient ID"},
            {"00100021", "LOIssuer of Patient ID"},
            {"00100022", "CSType of Patient ID"},
            {"00100030", "DAPatient's Birth Date"},
            {"00100032", "TMPatient's Birth Time"},
            {"00100040", "CSPatient's Sex"},
            {"00100050", "SQPatient's Insurance Plan Code Sequence"},
            {"00100101", "SQPatient's Primary Language Code Sequence"},
            {"00100102", "SQPatient's Primary Language Modifier Code Sequence"},
            {"00101000", "LOOther Patient IDs"},
            {"00101001", "PNOther Patient Names"},
            {"00101005", "PNPatient's Birth Name"},
            {"00101010", "ASPatient's Age"},
            {"00101020", "DSPatient's Size"},
            {"00101030", "DSPatient's Weight"},
            {"00101040", "LOPatient's Address"},
            {"00101050", "LOInsurance Plan Identification"},
            {"00102000", "LOMedical Alerts"},
            {"00102110", "LOAllergies"},
            {"00102150", "LOCountry of Residence"},
            {"00102152", "LORegion of Residence"},
            {"00102154", "SHPatient's Telephone Numbers"},
            {"00102160", "SHEthnic Group"},
            {"00102180", "SHOccupation"},
            {"001021A0", "CSSmoking Status"},
            {"001021B0", "LTAdditional Patient History"},
			{"00102201", "LOPatient Species Description"},
			{"00102203", "CSPatient Sex Neutered"},
			{"00102292", "LOPatient Breed Description"},
			{"00102297", "PNResponsible Person"},
			{"00102298", "CSResponsible Person Role"},
			{"00102299", "CSResponsible Organization"},
            {"00104000", "LTPatient Comments"},

            {"00180010", "LOContrast/Bolus Agent"},
            {"00180015", "CSBody Part Examined"},
            {"00180020", "CSScanning Sequence"},
            {"00180021", "CSSequence Variant"},
            {"00180022", "CSScan Options"},
            {"00180023", "CSMR Acquisition Type"},
            {"00180024", "SHSequence Name"},
            {"00180025", "CSAngio Flag"},
            {"00180030", "LORadionuclide"},
            {"00180031", "LORadiopharmaceutical"},
            {"00180032", "DSEnergy Window Centerline"},
            {"00180033", "DSEnergy Window Total Width"},
            {"00180034", "LOIntervention Drug Name"},
            {"00180035", "TMIntervention Drug Start Time"},
            {"00180040", "ISCine Rate"},
            {"00180050", "DSSlice Thickness"},
            {"00180060", "DSKVP"},
            {"00180070", "ISCounts Accumulated"},
            {"00180071", "CSAcquisition Termination Condition"},
            {"00180072", "DSEffective Duration"},
            {"00180073", "CSAcquisition Start Condition"},
            {"00180074", "ISAcquisition Start Condition Data"},
            {"00180075", "ISAcquisition Termination Condition Data"},
            {"00180080", "DSRepetition Time"},
            {"00180081", "DSEcho Time"},
            {"00180082", "DSInversion Time"},
            {"00180083", "DSNumber of Averages"},
            {"00180084", "DSImaging Frequency"},
            {"00180085", "SHImaged Nucleus"},
            {"00180086", "ISEcho Numbers(s)"},
            {"00180087", "DSMagnetic Field Strength"},
            {"00180088", "DSSpacing Between Slices"},
            {"00180089", "ISNumber of Phase Encoding Steps"},
            {"00180090", "DSData Collection Diameter"},
            {"00180091", "ISEcho Train Length"},
            {"00180093", "DSPercent Sampling"},
            {"00180094", "DSPercent Phase Field of View"},
            {"00180095", "DSPixel Bandwidth"},
            {"00181000", "LODevice Serial Number"},
            {"00181004", "LOPlate ID"},
            {"00181010", "LOSecondary Capture Device ID"},
            {"00181012", "DADate of Secondary Capture"},
            {"00181014", "TMTime of Secondary Capture"},
            {"00181016", "LOSecondary Capture Device Manufacturer"},
            {"00181018", "LOSecondary Capture Device Manufacturer's Model Name"},
            {"00181019", "LOSecondary Capture Device Software Versions"},
            {"00181020", "LOSoftware Versions(s)"},
            {"00181022", "SHVideo Image Format Acquired"},
            {"00181023", "LODigital Image Format Acquired"},
            {"00181030", "LOProtocol Name"},
            {"00181040", "LOContrast/Bolus Route"},
            {"00181041", "DSContrast/Bolus Volume"},
            {"00181042", "TMContrast/Bolus Start Time"},
            {"00181043", "TMContrast/Bolus Stop Time"},
            {"00181044", "DSContrast/Bolus Total Dose"},
            {"00181045", "ISSyringe Counts"},
            {"00181050", "DSSpatial Resolution"},
            {"00181060", "DSTrigger Time"},
            {"00181061", "LOTrigger Source or Type"},
            {"00181062", "ISNominal Interval"},
            {"00181063", "DSFrame Time"},
            {"00181064", "LOCardiac Framing Type"},
            {"00181065", "DSFrame Time Vector"},
            {"00181066", "DSFrame Delay"},
            {"00181070", "LORadiopharmaceutical Route"},
            {"00181071", "DSRadiopharmaceutical Volume"},
            {"00181072", "TMRadiopharmaceutical Start Time"},
            {"00181073", "TMRadiopharmaceutical Stop Time"},
            {"00181074", "DSRadionuclide Total Dose"},
            {"00181075", "DSRadionuclide Half Life"},
            {"00181076", "DSRadionuclide Positron Fraction"},
            {"00181080", "CSBeat Rejection Flag"},
            {"00181081", "ISLow R-R Value"},
            {"00181082", "ISHigh R-R Value"},
            {"00181083", "ISIntervals Acquired"},
            {"00181084", "ISIntervals Rejected"},
            {"00181085", "LOPVC Rejection"},
            {"00181086", "ISSkip Beats"},
            {"00181088", "ISHeart Rate"},
            {"00181090", "ISCardiac Number of Images"},
            {"00181094", "ISTrigger Window"},
            {"00181100", "DSReconstruction Diameter"},
            {"00181110", "DSDistance Source to Detector"},
            {"00181111", "DSDistance Source to Patient"},
            {"00181120", "DSGantry/Detector Tilt"},
            {"00181130", "DSTable Height"},
            {"00181131", "DSTable Traverse"},
            {"00181140", "CSRotation Direction"},
            {"00181141", "DSAngular Position"},
            {"00181142", "DSRadial Position"},
            {"00181143", "DSScan Arc"},
            {"00181144", "DSAngular Step"},
            {"00181145", "DSCenter of Rotation Offset"},
            {"00181146", "DSRotation Offset"},
            {"00181147", "CSField of View Shape"},
            {"00181149", "ISField of View Dimensions(s)"},
            {"00181150", "ISExposure Time"},
            {"00181151", "ISX-ray Tube Current"},
            {"00181152", "ISExposure"},
            {"00181153", "ISExposure in uAs"},
            {"00181154", "DSAverage Pulse Width"},
            {"00181155", "CSRadiation Setting"},
            {"00181156", "CSRectification Type"},
            {"0018115A", "CSRadiation Mode"},
            {"0018115E", "DSImage and Fluoroscopy Area Dose Product"},
            {"00181160", "SHFilter Type"},
            {"00181161", "LOType of Filters"},
            {"00181162", "DSIntensifier Size"},
            {"00181164", "DSImager Pixel Spacing"},
            {"00181166", "CSGrid"},
            {"00181170", "ISGenerator Power"},
            {"00181180", "SHCollimator/grid Name"},
            {"00181181", "CSCollimator Type"},
            {"00181182", "ISFocal Distance"},
            {"00181183", "DSX Focus Center"},
            {"00181184", "DSY Focus Center"},
            {"00181190", "DSFocal Spot(s)"},
            {"00181191", "CSAnode Target Material"},
            {"001811A0", "DSBody Part Thickness"},
            {"001811A2", "DSCompression Force"},
            {"00181200", "DADate of Last Calibration"},
            {"00181201", "TMTime of Last Calibration"},
            {"00181210", "SHConvolution Kernel"},
            {"00181242", "ISActual Frame Duration"},
            {"00181243", "ISCount Rate"},
            {"00181250", "SHReceive Coil Name"},
            {"00181251", "SHTransmit Coil Name"},
            {"00181260", "SHPlate Type"},
            {"00181261", "LOPhosphor Type"},
            {"00181300", "ISScan Velocity"},
            {"00181301", "CSWhole Body Technique"},
            {"00181302", "ISScan Length"},
            {"00181310", "USAcquisition Matrix"},
            {"00181312", "CSIn-plane Phase Encoding Direction"},
            {"00181314", "DSFlip Angle"},
            {"00181315", "CSVariable Flip Angle Flag"},
            {"00181316", "DSSAR"},
            {"00181318", "DSdB/dt"},
            {"00181400", "LOAcquisition Device Processing Description"},
            {"00181401", "LOAcquisition Device Processing Code"},
            {"00181402", "CSCassette Orientation"},
            {"00181403", "CSCassette Size"},
            {"00181404", "USExposures on Plate"},
            {"00181405", "ISRelative X-Ray Exposure"},
            {"00181450", "CSColumn Angulation"},
            {"00181500", "CSPositioner Motion"},
            {"00181508", "CSPositioner Type"},
            {"00181510", "DSPositioner Primary Angle"},
            {"00181511", "DSPositioner Secondary Angle"},
            {"00181520", "DSPositioner Primary Angle Increment"},
            {"00181521", "DSPositioner Secondary Angle Increment"},
            {"00181530", "DSDetector Primary Angle"},
            {"00181531", "DSDetector Secondary Angle"},
            {"00181600", "CSShutter Shape"},
            {"00181602", "ISShutter Left Vertical Edge"},
            {"00181604", "ISShutter Right Vertical Edge"},
            {"00181606", "ISShutter Upper Horizontal Edge"},
            {"00181608", "ISShutter Lower Horizontal Edge"},
            {"00181610", "ISCenter of Circular Shutter"},
            {"00181612", "ISRadius of Circular Shutter"},
            {"00181620", "ISVertices of the Polygonal Shutter"},
            {"00181700", "ISCollimator Shape"},
            {"00181702", "ISCollimator Left Vertical Edge"},
            {"00181704", "ISCollimator Right Vertical Edge"},
            {"00181706", "ISCollimator Upper Horizontal Edge"},
            {"00181708", "ISCollimator Lower Horizontal Edge"},
            {"00181710", "ISCenter of Circular Collimator"},
            {"00181712", "ISRadius of Circular Collimator"},
            {"00181720", "ISVertices of the Polygonal Collimator"},
            {"00185000", "SHOutput Power"},
            {"00185010", "LOTransducer Data"},
            {"00185012", "DSFocus Depth"},
            {"00185020", "LOProcessing Function"},
            {"00185021", "LOPostprocessing Function"},
            {"00185022", "DSMechanical Index"},
            {"00185024", "DSBone Thermal Index"},
            {"00185026", "DSCranial Thermal Index"},
            {"00185027", "DSSoft Tissue Thermal Index"},
            {"00185028", "DSSoft Tissue-focus Thermal Index"},
            {"00185029", "DSSoft Tissue-surface Thermal Index"},
            {"00185050", "ISDepth of Scan Field"},
            {"00185100", "CSPatient Position"},
            {"00185101", "CSView Position"},
            {"00185104", "SQProjection Eponymous Name Code Sequence"},
            {"00185210", "DSImage Transformation Matrix"},
            {"00185212", "DSImage Translation Vector"},
            {"00186000", "DSSensitivity"},
            {"00186011", "SQSequence of Ultrasound Regions"},
            {"00186012", "USRegion Spatial Format"},
            {"00186014", "USRegion Data Type"},
            {"00186016", "ULRegion Flags"},
            {"00186018", "ULRegion Location Min X0"},
            {"0018601A", "ULRegion Location Min Y0"},
            {"0018601C", "ULRegion Location Max X1"},
            {"0018601E", "ULRegion Location Max Y1"},
            {"00186020", "SLReference Pixel X0"},
            {"00186022", "SLReference Pixel Y0"},
            {"00186024", "USPhysical Units X Direction"},
            {"00186026", "USPhysical Units Y Direction"},
            {"00181628", "FDReference Pixel Physical Value X"},
            {"0018602A", "FDReference Pixel Physical Value Y"},
            {"0018602C", "FDPhysical Delta X"},
            {"0018602E", "FDPhysical Delta Y"},
            {"00186030", "ULTransducer Frequency"},
            {"00186031", "CSTransducer Type"},
            {"00186032", "ULPulse Repetition Frequency"},
            {"00186034", "FDDoppler Correction Angle"},
            {"00186036", "FDSteering Angle"},
            {"00186038", "ULDoppler Sample Volume X Position (Retired)"},
            {"00186039", "SLDoppler Sample Volume X Position"},
            {"0018603A", "ULDoppler Sample Volume Y Position (Retired)"},
            {"0018603B", "SLDoppler Sample Volume Y Position"},
            {"0018603C", "ULTM-Line Position X0 (Retired)"},
            {"0018603D", "SLTM-Line Position X0"},
            {"0018603E", "ULTM-Line Position Y0 (Retired)"},
            {"0018603F", "SLTM-Line Position Y0"},
            {"00186040", "ULTM-Line Position X1 (Retired)"},
            {"00186041", "SLTM-Line Position X1"},
            {"00186042", "ULTM-Line Position Y1 (Retired)"},
            {"00186043", "SLTM-Line Position Y1"},
            {"00186044", "USPixel Component Organization"},
            {"00186046", "ULPixel Component Mask"},
            {"00186048", "ULPixel Component Range Start"},
            {"0018604A", "ULPixel Component Range Stop"},
            {"0018604C", "USPixel Component Physical Units"},
            {"0018604E", "USPixel Component Data Type"},
            {"00186050", "ULNumber of Table Break Points"},
            {"00186052", "ULTable of X Break Points"},
            {"00186054", "FDTable of Y Break Points"},
            {"00186056", "ULNumber of Table Entries"},
            {"00186058", "ULTable of Pixel Values"},
            {"0018605A", "ULTable of Parameter Values"},
            {"00187000", "CSDetector Conditions Nominal Flag"},
            {"00187001", "DSDetector Temperature"},
            {"00187004", "CSDetector Type"},
            {"00187005", "CSDetector Configuration"},
            {"00187006", "LTDetector Description"},
            {"00187008", "LTDetector Mode"},
            {"0018700A", "SHDetector ID"},
            {"0018700C", "DADate of Last Detector Calibration"},
            {"0018700E", "TMTime of Last Detector Calibration"},
            {"00187010", "ISExposures on Detector Since Last Calibration"},
            {"00187011", "ISExposures on Detector Since Manufactured"},
            {"00187012", "DSDetector Time Since Last Exposure"},
            {"00187014", "DSDetector Active Time"},
            {"00187016", "DSDetector Activation Offset From Exposure"},
            {"0018701A", "DSDetector Binning"},
            {"00187020", "DSDetector Element Physical Size"},
            {"00187022", "DSDetector Element Spacing"},
            {"00187024", "CSDetector Active Shape"},
            {"00187026", "DSDetector Active Dimension(s)"},
            {"00187028", "DSDetector Active Origin"},
            {"00187030", "DSField of View Origin"},
            {"00187032", "DSField of View Rotation"},
            {"00187034", "CSField of View Horizontal Flip"},
            {"00187040", "LTGrid Absorbing Material"},
            {"00187041", "LTGrid Spacing Material"},
            {"00187042", "DSGrid Thickness"},
            {"00187044", "DSGrid Pitch"},
            {"00187046", "ISGrid Aspect Ratio"},
            {"00187048", "DSGrid Period"},
            {"0018704C", "DSGrid Focal Distance"},
            {"00187050", "LTFilter Material"},
            {"00187052", "DSFilter Thickness Minimum"},
            {"00187054", "DSFilter Thickness Maximum"},
            {"00187060", "CSExposure Control Mode"},
            {"00187062", "LTExposure Control Mode Description"},
            {"00187064", "CSExposure Status"},
            {"00187065", "DSPhototimer Setting"},

            {"0020000D", "UIStudy Instance UID"},
            {"0020000E", "UISeries Instance UID"},
            {"00200010", "SHStudy ID"},
            {"00200011", "ISSeries Number"},
            {"00200012", "ISAcquisition Number"},
            {"00200013", "ISInstance Number"},
            {"00200014", "ISIsotope Number"},
            {"00200015", "ISPhase Number"},
            {"00200016", "ISInterval Number"},
            {"00200017", "ISTime Slot Number"},
            {"00200018", "ISAngle Number"},
            {"00200020", "CSPatient Orientation"},
            {"00200022", "USOverlay Number"},
            {"00200024", "USCurve Number"},
            {"00200030", "DSImage Position"},
            {"00200032", "DSImage Position (Patient)"},
            {"00200037", "DSImage Orientation (Patient)"},
            {"00200050", "DSLocation"},
            {"00200052", "UIFrame of Reference UID"},
            {"00200060", "CSLaterality"},
            {"00200070", "LOImage Geometry Type"},
            {"00200080", "UIMasking Image"},
            {"00200100", "ISTemporal Position Identifier"},
            {"00200105", "ISNumber of Temporal Positions"},
            {"00200110", "DSTemporal Resolution"},
            {"00201000", "ISSeries in Study"},
            {"00201002", "ISImages in Acquisition"},
            {"00201004", "ISAcquisitions in Study"},
            {"00201040", "LOPosition Reference Indicator"},
            {"00201041", "DSSlice Location"},
            {"00201070", "ISOther Study Numbers"},
            {"00201200", "ISNumber of Patient Related Studies"},
            {"00201202", "ISNumber of Patient Related Series"},
            {"00201204", "ISNumber of Patient Related Instances"},
            {"00201206", "ISNumber of Study Related Series"},
            {"00201208", "ISNumber of Study Related Instances"},
            {"00204000", "LTImage Comments"},

            {"00280002", "USSamples per Pixel"},
            {"00280004", "CSPhotometric Interpretation"},
            {"00280006", "USPlanar Configuration"},
            {"00280008", "ISNumber of Frames"},
            {"00280009", "ATFrame Increment Pointer"},
            {"00280010", "USRows"},
            {"00280011", "USColumns"},
            {"00280030", "DSPixel Spacing"},
            {"00280031", "DSZoom Factor"},
            {"00280032", "DSZoom Center"},
            {"00280034", "ISPixel Aspect Ratio"},
            {"00280051", "CSCorrected Image"},
            {"00280100", "USBits Allocated"},
            {"00280101", "USBits Stored"},
            {"00280102", "USHigh Bit"},
            {"00280103", "USPixel Representation"},
            {"00280106", "USSmallest Image Pixel Value"},
            {"00280107", "USLargest Image Pixel Value"},
            {"00280108", "USSmallest Pixel Value in Series"},
            {"00280109", "USLargest Pixel Value in Series"},
            {"00280120", "USPixel Padding Value"},
            {"00280300", "CSQuality Control Image"},
            {"00280301", "CSBurned In Annotation"},
            {"00281040", "CSPixel Intensity Relationship"},
            {"00281041", "SSPixel Intensity Relationship Sign"},
            {"00281050", "DSWindow Center"},
            {"00281051", "DSWindow Width"},
            {"00281052", "DSRescale Intercept"},
            {"00281053", "DSRescale Slope"},
            {"00281054", "LORescale Type"},
            {"00281055", "LOWindow Center & Width Explanation"},
            {"00281101", "USRed Palette Color Lookup Table Descriptor"},
            {"00281102", "USGreen Palette Color Lookup Table Descriptor"},
            {"00281103", "USBlue Palette Color Lookup Table Descriptor"},
            {"00281104", "USAlpha Palette Color Lookup Table Descriptor"},
            {"00281201", "OWRed Palette Color Lookup Table Data"},
            {"00281202", "OWGreen Palette Color Lookup Table Data"},
            {"00281203", "OWBlue Palette Color Lookup Table Data"},
            {"00281204", "OWAlpha Palette Color Lookup Table Data"},
            {"00282110", "CSLossy Image Compression"},
            {"00283000", "SQModality LUT Sequence"},
            {"00283002", "USLUT Descriptor"},
            {"00283003", "LOLUT Explanation"},
            {"00283004", "LOModality LUT Type"},
            {"00283006", "USLUT Data"},
            {"00283010", "SQVOI LUT Sequence"},

            {"0032000A", "CSStudy Status ID"},
            {"0032000C", "CSStudy Priority ID"},
            {"00320012", "LOStudy ID Issuer"},
            {"00320032", "DAStudy Verified Date"},
            {"00320033", "TMStudy Verified Time"},
            {"00320034", "DAStudy Read Date"},
            {"00320035", "TMStudy Read Time"},
            {"00321000", "DAScheduled Study Start Date"},
            {"00321001", "TMScheduled Study Start Time"},
            {"00321010", "DAScheduled Study Stop Date"},
            {"00321011", "TMScheduled Study Stop Time"},
            {"00321020", "LOScheduled Study Location"},
            {"00321021", "AEScheduled Study Location AE Title"},
            {"00321030", "LOReason for Study"},
            {"00321032", "PNRequesting Physician"},
            {"00321033", "LORequesting Service"},
            {"00321040", "DAStudy Arrival Date"},
            {"00321041", "TMStudy Arrival Time"},
            {"00321050", "DAStudy Completion Date"},
            {"00321051", "TMStudy Completion Time"},
            {"00321055", "CSStudy Component Status ID"},
            {"00321060", "LORequested Procedure Description"},
            {"00321064", "SQRequested Procedure Code Sequence"},
            {"00321070", "LORequested Contrast Agent"},
            {"00324000", "LTStudy Comments"},

            {"00400001", "AEScheduled Station AE Title"},
            {"00400002", "DAScheduled Procedure Step Start Date"},
            {"00400003", "TMScheduled Procedure Step Start Time"},
            {"00400004", "DAScheduled Procedure Step End Date"},
            {"00400005", "TMScheduled Procedure Step End Time"},
            {"00400006", "PNScheduled Performing Physician's Name"},
            {"00400007", "LOScheduled Procedure Step Description"},
            {"00400008", "SQScheduled Protocol Code Sequence"},
            {"00400009", "SHScheduled Procedure Step ID"},
            {"00400010", "SHScheduled Station Name"},
            {"00400011", "SHScheduled Procedure Step Location"},
            {"00400012", "LOPre-Medication"},
            {"00400020", "CSScheduled Procedure Step Status"},
            {"00400100", "SQScheduled Procedure Step Sequence"},
            {"00400220", "SQReferenced Non-Image Composite SOP Instance Sequence"},
            {"00400241", "AEPerformed Station AE Title"},
            {"00400242", "SHPerformed Station Name"},
            {"00400243", "SHPerformed Location"},
            {"00400244", "DAPerformed Procedure Step Start Date"},
            {"00400245", "TMPerformed Procedure Step Start Time"},
            {"00400250", "DAPerformed Procedure Step End Date"},
            {"00400251", "TMPerformed Procedure Step End Time"},
            {"00400252", "CSPerformed Procedure Step Status"},
            {"00400253", "SHPerformed Procedure Step ID"},
            {"00400254", "LOPerformed Procedure Step Description"},
            {"00400255", "LOPerformed Procedure Type Description"},
            {"00400260", "SQPerformed Protocol Code Sequence"},
            {"00400270", "SQScheduled Step Attributes Sequence"},
            {"00400275", "SQRequest Attributes Sequence"},
            {"00400280", "STComments on the Performed Procedure Step"},
            {"00400293", "SQQuantity Sequence"},
            {"00400294", "DSQuantity"},
            {"00400295", "SQMeasuring Units Sequence"},
            {"00400296", "SQBilling Item Sequence"},
            {"00400300", "USTotal Time of Fluoroscopy"},
            {"00400301", "USTotal Number of Exposures"},
            {"00400302", "USEntrance Dose"},
            {"00400303", "USExposed Area"},
            {"00400306", "DSDistance Source to Entrance"},
            {"00400307", "DSDistance Source to Support"},
            {"00400310", "STComments on Radiation Dose"},
            {"00400312", "DSX-Ray Output"},
            {"00400314", "DSHalf Value Layer"},
            {"00400316", "DSOrgan Dose"},
            {"00400318", "CSOrgan Exposed"},
            {"00400320", "SQBilling Procedure Step Sequence"},
            {"00400321", "SQFilm Consumption Sequence"},
            {"00400324", "SQBilling Supplies and Devices Sequence"},
            {"00400330", "SQReferenced Procedure Step Sequence"},
            {"00400340", "SQPerformed Series Sequence"},
            {"00400400", "LTComments on the Scheduled Procedure Step"},
            {"0040050A", "LOSpecimen Accession Number"},
            {"00400550", "SQSpecimen Sequence"},
            {"00400551", "LOSpecimen Identifier"},
            {"00400555", "SQAcquisition Context Sequence"},
            {"00400556", "STAcquisition Context Description"},
            {"0040059A", "SQSpecimen Type Code Sequence"},
            {"004006FA", "LOSlide Identifier"},
            {"0040071A", "SQImage Center Point Coordinates Sequence"},
            {"0040072A", "DSX Offset in Slide Coordinate System"},
            {"0040073A", "DSY Offset in Slide Coordinate System"},
            {"0040074A", "DSZ Offset in Slide Coordinate System"},
            {"004008D8", "SQPixel Spacing Sequence"},
            {"004008DA", "SQCoordinate System Axis Code Sequence"},
            {"004008EA", "SQMeasurement Units Code Sequence"},
            {"00401001", "SHRequested Procedure ID"},
            {"00401002", "LOReason for the Requested Procedure"},
            {"00401003", "SHRequested Procedure Priority"},
            {"00401004", "LOPatient Transport Arrangements"},
            {"00401005", "LORequested Procedure Location"},
            {"00401006", "SHPlacer Order Number / Procedure"},
            {"00401007", "SHFiller Order Number / Procedure"},
            {"00401008", "LOConfidentiality Code"},
            {"00401009", "SHReporting Priority"},
            {"00401010", "PNNames of Intended Recipients of Results"},
            {"00401400", "LTRequested Procedure Comments"},
            {"00402001", "LOReason for the Imaging Service Request"},
            {"00402004", "DAIssue Date of Imaging Service Request"},
            {"00402005", "TMIssue Time of Imaging Service Request"},
            {"00402006", "SHPlacer Order Number / Imaging Service Request (Retired)"},
            {"00402007", "SHFiller Order Number / Imaging Service Request (Retired)"},
            {"00402008", "PNOrder Entered By"},
            {"00402009", "SHOrder Enterer's Location"},
            {"00402010", "SHOrder Callback Phone Number"},
            {"00402016", "LOPlacer Order Number / Imaging Service Request"},
            {"00402017", "LOFiller Order Number / Imaging Service Request"},
            {"00402400", "LTImaging Service Request Comments"},
            {"00403001", "LOConfidentiality Constraint on Patient Data Description"},
            {"00408302", "DSEntrance Dose in mGy"},
            {"0040A010", "CSRelationship Type"},
            {"0040A027", "LOVerifying Organization"},
            {"0040A030", "DTVerification Date Time"},
            {"0040A032", "DTObservation Date Time"},
            {"0040A040", "CSValue Type"},
            {"0040A043", "SQConcept Name Code Sequence"},
            {"0040A050", "CSContinuity Of Content"},
            {"0040A073", "SQVerifying Observer Sequence"},
            {"0040A075", "PNVerifying Observer Name"},
            {"0040A088", "SQVerifying Observer Identification Code Sequence"},
            {"0040A0B0", "USReferenced Waveform Channels"},
            {"0040A120", "DTDateTime"},
            {"0040A121", "DADate"},
            {"0040A122", "TMTime"},
            {"0040A123", "PNPerson Name"},
            {"0040A124", "UIUID"},
            {"0040A130", "CSTemporal Range Type"},
            {"0040A132", "ULReferenced Sample Positions"},
            {"0040A136", "USReferenced Frame Numbers"},
            {"0040A138", "DSReferenced Time Offsets"},
            {"0040A13A", "DTReferenced DateTime"},
            {"0040A160", "UTText Value"},
            {"0040A168", "SQConcept Code Sequence"},
            {"0040A180", "USAnnotation Group Number"},
            {"0040A195", "SQModifier Code Sequence"},
            {"0040A300", "SQMeasured Value Sequence"},
            {"0040A30A", "DSNumeric Value"},
            {"0040A360", "SQPredecessor Documents Sequence"},
            {"0040A370", "SQReferenced Request Sequence"},
            {"0040A372", "SQPerformed Procedure Code Sequence"},
            {"0040A375", "SQCurrent Requested Procedure Evidence Sequence"},
            {"0040A385", "SQPertinent Other Evidence Sequence"},
            {"0040A491", "CSCompletion Flag"},
            {"0040A492", "LOCompletion Flag Description"},
            {"0040A493", "CSVerification Flag"},
            {"0040A504", "SQContent Template Sequence"},
            {"0040A525", "SQIdentical Documents Sequence"},
            {"0040A730", "SQContent Sequence"},
            {"0040B020", "SQWaveform Annotation Sequence"},
            {"0040DB00", "CSTemplate Identifier"},
            {"0040DB06", "DTTemplate Version"},
            {"0040DB07", "DTTemplate Local Version"},
            {"0040DB0B", "CSTemplate Extension Flag"},
            {"0040DB0C", "UITemplate Extension Organization UID"},
            {"0040DB0D", "UITemplate Extension Creator UID"},
            {"0040DB73", "ULReferenced Content Item Identifier"},

            {"00540011", "USNumber of Energy Windows"},
            {"00540012", "SQEnergy Window Information Sequence"},
            {"00540013", "SQEnergy Window Range Sequence"},
            {"00540014", "DSEnergy Window Lower Limit"},
            {"00540015", "DSEnergy Window Upper Limit"},
            {"00540016", "SQRadiopharmaceutical Information Sequence"},
            {"00540017", "ISResidual Syringe Counts"},
            {"00540018", "SHEnergy Window Name"},
            {"00540020", "USDetector Vector"},
            {"00540021", "USNumber of Detectors"},
            {"00540022", "SQDetector Information Sequence"},
            {"00540030", "USPhase Vector"},
            {"00540031", "USNumber of Phases"},
            {"00540032", "SQPhase Information Sequence"},
            {"00540033", "USNumber of Frames in Phase"},
            {"00540036", "ISPhase Delay"},
            {"00540038", "ISPause Between Frames"},
            {"00540039", "CSPhase Description"},
            {"00540050", "USRotation Vector"},
            {"00540051", "USNumber of Rotations"},
            {"00540052", "SQRotation Information Sequence"},
            {"00540053", "USNumber of Frames in Rotation"},
            {"00540060", "USR-R Interval Vector"},
            {"00540061", "USNumber of R-R Intervals"},
            {"00540062", "SQGated Information Sequence"},
            {"00540063", "SQData Information Sequence"},
            {"00540070", "USTime Slot Vector"},
            {"00540071", "USNumber of Time Slots"},
            {"00540072", "SQTime Slot Information Sequence"},
            {"00540073", "DSTime Slot Time"},
            {"00540080", "USSlice Vector"},
            {"00540081", "USNumber of Slices"},
            {"00540090", "USAngular View Vector"},
            {"00540100", "USTime Slice Vector"},
            {"00540101", "USNumber of Time Slices"},
            {"00540200", "DSStart Angle"},
            {"00540202", "CSType of Detector Motion"},
            {"00540210", "ISTrigger Vector"},
            {"00540211", "USNumber of Triggers in Phase"},
            {"00540220", "SQView Code Sequence"},
            {"00540222", "SQView Modifier Code Sequence"},
            {"00540300", "SQRadionuclide Code Sequence"},
            {"00540302", "SQAdministration Route Code Sequence"},
            {"00540304", "SQRadiopharmaceutical Code Sequence"},
            {"00540306", "SQCalibration Data Sequence"},
            {"00540308", "USEnergy Window Number"},
            {"00540400", "SHImage ID"},
            {"00540410", "SQPatient Orientation Code Sequence"},
            {"00540412", "SQPatient Orientation Modifier Code Sequence"},
            {"00540414", "SQPatient Gantry Relationship Code Sequence"},
            {"00540500", "CSSlice Progression Direction"},
            {"00541000", "CSSeries Type"},
            {"00541001", "CSUnits"},
            {"00541002", "CSCounts Source"},
            {"00541004", "CSReprojection Method"},
            {"00541100", "CSRandoms Correction Method"},
            {"00541101", "LOAttenuation Correction Method"},
            {"00541102", "CSDecay Correction"},
            {"00541103", "LOReconstruction Method"},
            {"00541104", "LODetector Lines of Response Used"},
            {"00541105", "LOScatter Correction Method"},
            {"00541200", "DSAxial Acceptance"},
            {"00541201", "ISAxial Mash"},
            {"00541202", "ISTransverse Mash"},
            {"00541203", "DSDetector Element Size"},
            {"00541210", "DSCoincidence Window Width"},
            {"00541220", "CSSecondary Counts Type"},
            {"00541300", "DSFrame Reference Time"},
            {"00541310", "ISPrimary (Prompts) Counts Accumulated"},
            {"00541311", "ISSecondary Counts Accumulated"},
            {"00541320", "DSSlice Sensitivity Factor"},
            {"00541321", "DSDecay Factor"},
            {"00541322", "DSDose Calibration Factor"},
            {"00541323", "DSScatter Fraction Factor"},
            {"00541324", "DSDead Time Factor"},
            {"00541330", "USImage Index"},
            {"00541400", "CSCounts Included"},
            {"00541401", "CSDead Time Correction Flag"},

            {"20300010", "USAnnotation Position"},
            {"20300020", "LOText string"},

            {"20500010", "SQPresentation LUT Sequence"},
            {"20500020", "CSPresentation LUT Shape"},
            {"20500500", "SQReferenced Presentation LUT Sequence"},

            {"30020002", "SHRT Image Label"},
            {"30020003", "LORT Image Name"},
            {"30020004", "STRT Image Description"},
            {"3002000A", "CSReported Values Origin"},
            {"3002000C", "CSRT Image Plane"},
            {"3002000D", "DSX-Ray Image Receptor Translation"},
            {"3002000E", "DSX-Ray Image Receptor Angle"},
            {"30020010", "DSRT Image Orientation"},
            {"30020011", "DSImage Plane Pixel Spacing"},
            {"30020012", "DSRT Image Position"},
            {"30020020", "SHRadiation Machine Name"},
            {"30020022", "DSRadiation Machine SAD"},
            {"30020024", "DSRadiation Machine SSD"},
            {"30020026", "DSRT Image SID"},
            {"30020028", "DSSource to Reference Object Distance"},
            {"30020029", "ISFraction Number"},
            {"30020030", "SQExposure Sequence"},
            {"30020032", "DSMeterset Exposure"},
            {"30020034", "DSDiaphragm Position"},
            {"30020040", "SQFluence Map Sequence"},
            {"30020041", "CSFluence Data Source"},
            {"30020042", "DSFluence Data Scale"},

            {"30040001", "CSDVH Type"},
            {"30040002", "CSDose Units"},
            {"30040004", "CSDose Type"},
            {"30040006", "LODose Comment"},
            {"30040008", "DSNormalization Point"},
            {"3004000A", "CSDose Summation Type"},
            {"3004000C", "DSGrid Frame Offset Vector"},
            {"3004000E", "DSDose Grid Scaling"},
            {"30040010", "SQRT Dose ROI Sequence"},
            {"30040012", "DSDose Value"},
            {"30040014", "CSTissue Heterogeneity Correction"},
            {"30040040", "DSDVH Normalization Point"},
            {"30040042", "DSDVH Normalization Dose Value"},
            {"30040050", "SQDVH Sequence"},
            {"30040052", "DSDVH Dose Scaling"},
            {"30040054", "CSDVH Volume Units"},
            {"30040056", "ISDVH Number of Bins"},
            {"30040058", "DSDVH Data"},
            {"30040060", "SQDVH Referenced ROI Sequence"},
            {"30040062", "CSDVH ROI Contribution Type"},
            {"30040070", "DSDVH Minimum Dose"},
            {"30040072", "DSDVH Maximum Dose"},
            {"30040074", "DSDVH Mean Dose"},

            {"300A00B3", "CSPrimary Dosimeter Unit"},
            {"300A00F0", "ISNumber of Blocks"},
            {"300A011E", "DSGantry Angle"},
            {"300A0120", "DSBeam Limiting Device Angle"},
            {"300A0122", "DSPatient Support Angle"},
            {"300A0128", "DSTable Top Vertical Position"},
            {"300A0129", "DSTable Top Longitudinal Position"},
            {"300A012A", "DSTable Top Lateral Position"},
		
            {"300C0006", "ISReferenced Beam Number"},
            {"300C0008", "DSStart Cumulative Meterset Weight"},
            {"300C0022", "ISReferenced Fraction Group Number"},
        
            {"7FE00010", "OXPixel Data"}, // Represents OB or OW type of VR

            {"FFFEE000", "DLItem"},
            {"FFFEE00D", "DLItem Delimitation Item"},
            {"FFFEE0DD", "DLSequence Delimitation Item"} 
        };
    }//DICOMDictionary
#endregion
}//Class DICOMDeconder
