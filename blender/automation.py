"""Headless Blender operations. Blender calls: --python automation.py -- OP INPUT OUTPUT ADDON_DIR."""
import json
import sys
from pathlib import Path

import bpy


def load_scene(path, addon_dir):
    extension = path.suffix.lower()
    if addon_dir:
        sys.path.insert(0, str(Path(addon_dir).resolve()))
    if extension in {".glb", ".gltf"}:
        bpy.ops.import_scene.gltf(filepath=str(path))
    elif extension == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(path))
    elif extension == ".blend":
        bpy.ops.wm.open_mainfile(filepath=str(path))
    elif extension in {".gmt", ".gmd"}:
        raise RuntimeError("GMT/GMD import needs a compatible Blender add-on operator. Configure and run the add-on directly, then save a .blend project for this pipeline.")
    else:
        raise RuntimeError(f"Unsupported animation input: {extension}")


def inspect(output):
    data = {"skeletons": [], "animations": []}
    for obj in bpy.data.objects:
        if obj.type == "ARMATURE":
            data["skeletons"].append({"name": obj.name, "bones": [bone.name for bone in obj.data.bones]})
    for action in bpy.data.actions:
        data["animations"].append({"name": action.name, "frames": [int(action.frame_range[0]), int(action.frame_range[1])]})
    output.write_text(json.dumps(data, indent=2), encoding="utf-8")


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    if len(args) < 3:
        raise RuntimeError("Expected operation, input and output paths.")
    operation, input_name, output_name = args[:3]
    addon_dir = args[3] if len(args) > 3 else ""
    source, output = Path(input_name).resolve(), Path(output_name).resolve()
    if not source.is_file():
        raise FileNotFoundError(source)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    load_scene(source, addon_dir)
    output.parent.mkdir(parents=True, exist_ok=True)
    if operation == "inspect":
        inspect(output)
    elif operation == "export_glb":
        bpy.ops.export_scene.gltf(filepath=str(output), export_format="GLB")
    elif operation == "save_blend":
        bpy.ops.wm.save_as_mainfile(filepath=str(output))
    else:
        raise RuntimeError(f"Unsupported Blender operation: {operation}")
    print(f"Completed {operation}: {output}")


if __name__ == "__main__":
    main()
