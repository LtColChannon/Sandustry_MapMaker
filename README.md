# Sandustry Map Maker

Sandustry Map Maker is a small command-line tool that converts Sandustry save maps to images and back again.

The tool lets you:
- export the Sandustry world-map as a PNG,
- edit that PNG with any image editor,
- write the edited map back into a Sandustry save file.

## Usage
MapMaker.exe img2map game-file image [template_game-file] [--no-cleanup]
MapMaker.exe map2img game-file image

> [!note]
> Pass file names without extension. The tool appends .save, .png, and .json where needed.

- _map2img_ reads a Sandustry .save file and writes a .png image.
- _img2map_ reads a .png image and writes a Sandustry .save file.
- _template_game-file_ (optional, but recommended) is a JSON template for the save file when generating output.
- --no-cleanup (optional, not recommended) keeps existing fixtures and structures in the template instead of clearing them.

## Workflow

1. Create a new game in Sandustry.
2. Export the save file from Sandustry.
3. Run Map Maker to create an image from the save file.
	e. g. _MapMaker.exe map2img a1bcde2f3g MyGameMap_
	This creates MyGameMap.png from your save file a1bcde2f3g.save.
4. Edit the image however you like (drawing in paint works just file)
5. Run Map Maker to inject your edited image back into a save file.
	e. g. _MapMaker.exe img2map a1bcde2f3g MyGameMap NewGame_
	This overwrites a1bcde2f3g.save using NewGame.json as a template and injecting MyGameMap.png as the map.
6. Import the resulting save file back into Sandustry.

## Templates

This repository includes the following example templates:
- NewGame.json is basically a new game, but without any buildings (so they don't interfere with your map)
- SandboxGame.json has the whole tech tree unlocked, provides an extra fast jetpack as well as pretty spicy digging tools

## Notes
- The tool maps known Sandustry materials to predefined colors.
- Unknown materials are preserved by encoding their value in the alpha channel of a fallback color.
- If unknown colors are contained in an imported image, the tool maps them to some known color.
