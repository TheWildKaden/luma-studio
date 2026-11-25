# Luma Studio

A local browser editor for Yakuza 0 fighter-command files. Luma Studio generates a replacement `fighter_command_main.cfc` from a base CFC and your animation edits.

## Run locally

From this checkout, run `python3 -m http.server 8000 --bind 127.0.0.1` and open `http://127.0.0.1:8000` in a modern browser. No package installation is required.

1. Browse for your extracted fighter-command CFC or drop it onto the file panel.
2. Choose a moveset and click **Edit** on a command. Search the animation IDs indexed from the file and select a replacement.
3. Click **Download .cfc** to generate `fighter_command_main.cfc`. Keep the original and install the replacement through your existing mod workflow.

The editor works with the CFCI 2.1 layout verified against a Yakuza 0 backup. It remaps existing direct-animation commands. The export preserves command logic and the source layout by writing only the selected animation references. Animation availability depends on the target moveset and the game assets it loads. Confirm each change in-game.

Files and edits stay in the browser session. Export before closing the tab.

## Windows desktop app

The .NET 10 WPF application is in `src/YakuzaMovesetCreator`. On Windows x64, run:

```powershell
dotnet publish src/YakuzaMovesetCreator/YakuzaMovesetCreator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
.\publish\Yakuza0MovesetCreator.exe
```

The app opens a `CFCI` 2.1 fighter command file, browses Kiryu and Majima movesets, remaps direct animation references, saves a JSON project, draws project combo branches, and exports a ZIP containing the edited CFC, original backup, and manual installation instructions. It keeps original CFC bytes apart from changed animation pointers. Undo and redo cover project edits. External executable locations are saved in local application data.

The Animation Studio runs bundled Python automation through Blender's background command line. It inspects skeletons and actions in `.blend`, `.fbx`, `.glb`, and `.gltf` inputs. It can save a `.blend` or export `.glb`. GMT and GMD conversion requires a compatible installed Blender add-on or external converter. FighterCommander and GMT Converter paths can be configured, but their command protocols are unverified and the app does not invoke them. Heat action references and notes can be stored in the project. Binary heat action editing, CFC combo rewiring, retargeting, newly authored animation curves, automatic game archive installation, and game playability validation are unsupported. Project combo branches are planning data and do not change exported CFC combat flow.

The Linux build validates C# compilation and the Windows target but cannot run the WPF interface. A Windows computer and the game are required to validate the final executable and mod in-game.
