using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BioLib;
using BioLib.Streams;

namespace spoondec {
	class Program {
		private const string VERSION = "2.0.1";
		private const string PROMPT_ID = "spondec_overwrite";
		private const string INSTALLER_SCRIPT_NAME = "#InstallerScript.ini";
		private static readonly byte[] LZMA_HEADER = { 0x5d, 0x00, 0x00, 0x80, 0x00 };

		private static string inputFile;
		private static string outputDirectory;
		private static FileStream fileStream;
		private static BinaryReader binaryReader;
		private static SevenZip.Compression.LZMA.Decoder lzmaDecoder;

		static void Main(string[] args) {
			const string USAGE = "[<options>...] <installer> [<output_directory>]";
			Bio.Header("spoondec - A Spoon Installer unpacker", VERSION, "2017-2022", "Extracts files from Spoon Installers", USAGE);

			ParseCommandLine(args);

			Bio.Cout("Opening installer");
			fileStream = File.OpenRead(inputFile);
			binaryReader = new BinaryReader(fileStream);
			var fileOffsets = fileStream.FindAll(LZMA_HEADER);
			if (fileOffsets.Count < 1) Bio.Error("No files could be found in the intaller. Either the file is not a Spoon Installer or it is not supported.", Bio.EXITCODE.INVALID_INPUT);

			Bio.Cout("Reading file list");
			var installerScriptOffset = fileOffsets.Last();
			fileStream.Position = installerScriptOffset;
			lzmaDecoder = new SevenZip.Compression.LZMA.Decoder();
			List<string> filePaths;

			using (var installerScript = new MemoryStream()) {
				try {
					DecompressLzma(installerScript, fileStream.Length - fileStream.Position);
				}
				catch (Exception) {
					Bio.Error("Failed to decompress installer script. Either the file is not a Spoon Installer or it is not supported.", Bio.EXITCODE.INVALID_INPUT);
				}

				filePaths = GetFileList(installerScript);

				Bio.Cout("Extracting files");
				Bio.Progress(INSTALLER_SCRIPT_NAME, 1, filePaths.Count + 1);
				var path = Bio.GetSafeOutputPath(outputDirectory, INSTALLER_SCRIPT_NAME);
				path = Bio.EnsureFileDoesNotExist(path, PROMPT_ID);
				if (path != null) installerScript.WriteToFile(path);
			}

			fileStream.MoveToStart();
			if (filePaths.Count != fileOffsets.Count - 1) Bio.Error("Failed to find all files in the installer. Either the file is not a Spoon Installer or it is not supported.", Bio.EXITCODE.INVALID_INPUT);

			var failed = ExtractFiles(filePaths, fileOffsets);
			if (failed < 1) {
				Bio.Cout("All OK");
			}
			else if (failed == filePaths.Count) {
				Bio.Error("Failed to extract installer. Either the file is not a Spoon Installer or it is not supported.", Bio.EXITCODE.NOT_SUPPORTED);
			}
			else {
				Bio.Warn("Some files failed to extract");
			}

			Bio.Pause();
		}

		static List<string> GetFileList(MemoryStream installerScript) {
			var regEx = new Regex(@"\[CopyFile\](?s:.*?)<Destination>\\(.+)");
			var matches = regEx.Matches(installerScript.ToUtf8String());

			var filePaths = new List<string>() { "#SetupImage.bmp", "#License.txt" };
			filePaths.AddRange(matches.Cast<Match>().Select((match) => match.Groups[1].Value));
			
			return filePaths;
		}

		static int ExtractFiles(List<string> filePaths, List<long> fileOffsets) {
			var totalFiles = filePaths.Count;
			var failed = 0;

			for (var i = 0; i < totalFiles; i++) {
				try {
					var filePath = filePaths[i];
					Bio.Progress(filePath, i + 2, totalFiles + 1);
					
					fileStream.Position = fileOffsets[i];
					filePath = Bio.GetSafeOutputPath(outputDirectory, filePath);
					using (var outputStream = Bio.CreateFile(filePath, PROMPT_ID)) {
						if (outputStream == null) continue;

						DecompressLzma(outputStream, fileOffsets[i + 1] - fileStream.Position);
					}
				}
				catch (Exception e) {
					Bio.Warn("Failed to extract file: " + e);
					failed++;
				}
			}

			return failed;
		}

		static void DecompressLzma(Stream outputStream, long compressedSize) {
			var properties = fileStream.Extract(5).ToArray();
			var decompressedSize = binaryReader.ReadInt64();
			Bio.Debug($"Extraction file at {fileStream.Position} - compressed: {compressedSize}, decompressed: {decompressedSize}");

			lzmaDecoder.SetDecoderProperties(properties);
			lzmaDecoder.Code(fileStream, outputStream, compressedSize, decompressedSize, null);
			outputStream.MoveToStart();
		}

		static void ParseCommandLine(string[] args) {
			var count = args.Length - 1;
			if (count < 0) Bio.Error("No input file specified.", Bio.EXITCODE.INVALID_INPUT);

			var path = args[count];
			
			if (File.Exists(path)) {
				inputFile = path;
				outputDirectory = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
			}
			else {
				outputDirectory = path;
				count--;
				if (count < 0) Bio.Error("Invalid input file specified.", Bio.EXITCODE.INVALID_INPUT);
				inputFile = args[count];
			}

			if (!File.Exists(inputFile)) Bio.Error("Invalid input file specified.", Bio.EXITCODE.IO_ERROR);

			for (var i = 0; i < count; i++) {
				var arg = args[i];
				Bio.Debug("Argument: " + arg);
				switch (arg) {
					default:
						Bio.Warn("Unknown command line option: " + arg);
						break;
				}
			}

			Bio.Debug("Input file: " + inputFile);
			Bio.Debug("Output directory: " + outputDirectory);
		}
	}
}
