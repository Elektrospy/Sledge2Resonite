# Sledge2Resonite

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) 
Enables users to import their own sledge file format based assets.

Only partial support of *.vmt and *.vtf files for now

## Self compile
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Clone this Repository (Don't forget the submodules!)
3. Implement the methods you need to for your project. Said methods are outlined in the comments.
4. Build the ValveResourceFormat and Sledge2Resonite solutions
5. Place your DLL's (Sledge2Resonite.dll, Sledge.Formats.dll, Sledge.Formats.Texture.dll) under "rml_mods".
  This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. 
  You can create it if it's missing, or if you launch the game once with ResoniteModLoader.
6. Start the game!

## Install prebuild
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Get prebuild *.dlls from release page
3. Place the DLL's (Sledge2Resonite.dll, Sledge.Formats.dll, Sledge.Formats.Texture.dll) under "rml_mods". 
  This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. 
  You can create it if it's missing, or if you launch the game once with ResoniteModLoader.
4. Start the game!

## Usage
Drag and drop the file you want to import onto the Resonite window.
The mod will try to do an in place conversion and spawn the result in front of you.

## Supported files
1. *.vtf - Valve Texture Format
  https://developer.valvesoftware.com/wiki/Valve_Texture_Format
2. *.vmt - Valve Material Type
  https://developer.valvesoftware.com/wiki/Material
3. *.raw - color correction LUT's
  https://developer.valvesoftware.com/wiki/Color_Correction

## Settings
* tintSpecular
  * Applies the $color values as tint directly to the specular texture rgb pixels
* importRows
  * Defines the number of texture quads on batch import per row
* generateTextureAtlas
  * Checks if a *.vtf file contains multiple frames and tries to generate a single line sprite sheet
  * If disabled, only the first frame will be imported
* SSBumpAutoConvert
  * Checks the image flags for "SSBump" and applies a conversion (see "Borrowed Code")

## Notes
* Materials
  * Currently all materials will be imported as pbs specular and specific shaders are not taken into account
* Textures
  * Image flags inside the header are partially ignored
    * We encountered a lot of texture files, with "broken" flags (e.g. albedo textures been flagged as normal map)
	
## Known issues
* Not all material settings are been utilized
* 2 and 4 way blend materials (e.g. terrain) will only import the first layer textures
  * color splat material conversion is not implemented at this stage
  * The color mask needs to be extracted and rebuild from the vertex colors of the displacement surfaces
* Specular color tints look weird
  * This is a Resonite specific issue, since the tint doesn't only affect the reflective parts.
  * Instead the rgb part of the specular is applied like a detail texture and will just be overlaid on top the albedo

### Contributors
Thanks go to these wonderful people
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
	<tbody>
		<tr>
			<td align="center"><a href="https://github.com/dfgHiatus"><img src="https://avatars.githubusercontent.com/u/51272212?v=4?s=100" width="100px;" alt="dfgHiatus"/><br /><sub><b>dfgHiatus</b></sub></a><br/></td>
			<td align="center"><a href="https://github.com/ukilop"><img src="https://avatars.githubusercontent.com/u/1341270?v=v?s=100" width="100px;" alt="ukilop"/><br /><sub><b>ukilop</b></sub></a><br/></td>
			<td align="center"><a href="https://github.com/marsmaantje"><img src="https://avatars.githubusercontent.com/u/60362806?v=4?s=100" width="100px;" alt="marsmaantje"/><br /><sub><b>marsmaantje</b></sub></a><br/></td>
		</tr>
	</tbody>
</table>
<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

### Borrowed Code
* ssbumpToNormal-Win
	* SSBumpmap to NormalMap conversion
	* https://github.com/rob5300/ssbumpToNormal-Win
