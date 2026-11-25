# Luma Studio

A local browser editor for Yakuza 0 fighter-command files. Luma Studio generates a replacement `fighter_command_main.cfc` from a base CFC and your animation edits.

## Run locally

From this checkout, run `python3 -m http.server 8000 --bind 127.0.0.1` and open `http://127.0.0.1:8000` in a modern browser. No package installation is required.

1. Browse for your extracted fighter-command CFC or drop it onto the file panel.
2. Choose a moveset and click **Edit** on a command. Search the animation IDs indexed from the file and select a replacement.
3. Click **Download .cfc** to generate `fighter_command_main.cfc`. Keep the original and install the replacement through your existing mod workflow.

The editor works with the CFCI 2.1 layout verified against a Yakuza 0 backup. It remaps existing direct-animation commands. The export preserves command logic and the source layout by writing only the selected animation references. Animation availability depends on the target moveset and the game assets it loads. Confirm each change in-game.

Files and edits stay in the browser session. Export before closing the tab.
