# This script is for editing all varients of sling textures MUCH faster
# It takes in a edited source file eg. folded.json
# Then modifies all .json files in the specified folder to match
#    the textures of the geometry faces with the textures with the same
#    names from the source file
# AKA it pushes the texture changes for source(folded) into the textures of the animation variants



import os
import json
import logging
import sys

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)
cwd = repr(os.getcwd())
logger.log(logging.INFO, f"Current working directory: {cwd}")

def load_json(file_path):
    """Loads JSON data from a given file path."""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            return json.load(f)
    except FileNotFoundError:
        print(f"Error: File not found at {file_path}")
        return None
    except json.JSONDecodeError:
        print(f"Warning: Skipping '{os.path.basename(file_path)}'. Invalid JSON format.")
        return None
    except Exception as e:
        print(f"An unexpected error occurred reading {os.path.basename(file_path)}: {e}")
        return None
    
def get_targets(node, result=None):
    if result is None:
        result = {}

    # Store the current node's data
    name = node["name"]
    texture = node["faces"]['up']['texture']
    if name is not None:
        result[name] = texture
                        

    # Recurse into children, if any
    children = node.get("children", [])
    
    for child in children:
        if child is not None:
            get_targets(child, result)

    return result

def update_nodes(node, source_data):
    name = node["name"]
    if name in source_data:
        for face in ["north","east","south","west","up","down"]:
            node["faces"][face]['texture'] = source_data[name]

    children = node.get("children", [])

    for child in children:
        # logger.info("Updating child: %s", child['name'])
        if child is not None:
            update_nodes(child, source_data)

    return node

def update_json_values(active_dir, source_filename):
    source_path = os.path.join(active_dir, source_filename)
    source_data = load_json(source_path)

    if not source_data:
        print("ERROR: Could not load the source file. Exiting.")
        return
    
    texture_defs = source_data["textures"]

    logger.info("Getting source textures from %s", source_path)
    updates = get_targets(source_data['elements'][0])
    logger.info("Source textures: %s", updates)
    for filename in os.listdir(active_dir):
        file_path = os.path.join(active_dir, filename)

        if not filename.endswith('.json') or filename == source_filename:
            continue

        logger.info("Updating %s", filename)
        target_data = load_json(file_path)
        if target_data is None:
            continue
        updated = update_nodes(target_data['elements'][0], updates)
        target_data['elements'][0] = updated
        target_data["textures"] = texture_defs
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(target_data, f, indent=4)


if __name__ == "__main__":
    # --- USAGE INSTRUCTIONS ---
    # You must pass two arguments when running this script from your terminal:
    # python json_updater.py <path/to/folder> <source_filename.json>
    
    if len(sys.argv) != 3:
        print("="*60)
        print("USAGE EXAMPLE:")
        print(f"python {sys.argv[0]} /path/to/your/data_folder replacement_values.json")
        print("\nNOTE: The script requires two positional arguments.")
        print("1. The root directory containing the JSON files.")
        print("2. The single source JSON file whose values will be used for replacement.")
        print("="*60)
    else:
        target_folder = sys.argv[1]
        source_file = sys.argv[2]

        if not os.path.isdir(target_folder):
            print(f"ERROR: The folder '{target_folder}' does not exist.")
        elif not source_file.lower().endswith(".json"):
             print("ERROR: Source file must be a JSON file.")
        else:
            update_json_values(target_folder, source_file)
