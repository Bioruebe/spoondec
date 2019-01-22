package;

import haxe.io.Bytes;
import lime.utils.LZMA;
import neko.Lib;
import sys.FileSystem;
import sys.io.File;

/**
 * ...
 * @author Bioruebe
 */
class Main {
	static inline var LZMA_HEADER = "5d00008000";
	
	static function main() {
		Bio.Header("Spoon Installer Extractor (spoondec)", "1.0.0", "2017", "An extractor for Spoon Installers", "<input_file> [<output_directory>]");
		Bio.Seperator();
		
		var args = Sys.args();
		if (args.length < 1) Bio.Error("Please specify an input file", 1);
		
		var inpPath = args[0];
		if (!FileSystem.exists(inpPath)) Bio.Error("Invalid input path: " + inpPath, 1);
		var outPath = Bio.PathAppendSeperator(args.length > 1? args[1]: getOutputDirectory(inpPath));
		var baseName = Bio.FileGetParts(inpPath).name + "_";
		
		// Read input file
		var inp = File.getBytes(inpPath);
		var hex = StringTools.replace(inp.toHex(), " ", "");
		//trace(hex);
		
		// Search LZMA header positions
		var offsets = new List<Int>();
		var startOffset = 0;
		var offset = hex.indexOf(LZMA_HEADER, startOffset);
		while (offset > -1) {
			offsets.push(Std.int(offset / 2));
			//trace(offset);
			startOffset = offset + 1;
			offset = hex.indexOf(LZMA_HEADER, startOffset);
		}
		
		offsets.push(inp.length);
		//trace(offsets);
		
		var outFiles = new List<Bytes>();
		var end = offsets.pop();
		var start = offsets.pop();
		while (start != null) {
			//trace(start + " - " + (end - start));
			outFiles.push(LZMA.decode(inp.sub(start, end - start)));
			end = start;
			start = offsets.pop();
		}
		
		var installerScriptBytes = outFiles.last();
		outFiles.remove(installerScriptBytes);
		if (!FileSystem.exists(outPath)) FileSystem.createDirectory(outPath);
		File.saveBytes(outPath + baseName + "image.bmp", outFiles.pop());
		File.saveBytes(outPath + baseName + "license.txt", outFiles.pop());
		File.saveBytes(outPath + baseName + "installer-script.txt", installerScriptBytes);
		
		var installerScript = installerScriptBytes.toString();
		var regEx = ~/\[CopyFile\](?s:.*?)<Destination>(.+)/;
		outPath = outPath.substr(0, outPath.length - 1);
		
		while (regEx.match(installerScript)) {
			var path = StringTools.replace(regEx.matched(1), "\r", "");
			var parts = Bio.FileGetParts(path);
			if (parts.directory.length != 1 && (!FileSystem.exists(parts.directory) || !FileSystem.isDirectory(parts.directory))) {
				FileSystem.createDirectory(outPath + parts.directory);
			}
			Bio.Cout("Extracting file " + outPath + path);
			File.saveBytes(outPath + path, outFiles.pop());
			installerScript = regEx.matchedRight();
		}
		
		Sys.println("");
		Bio.Cout("All ok");
	}
	
	static function getOutputDirectory(inputPath:String){
		var parts = Bio.FileGetParts(inputPath);
		return parts.directory + parts.name + "/";
	}
	
}