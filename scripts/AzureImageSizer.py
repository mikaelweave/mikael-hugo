# Libraries
import os, pathlib, yaml, re, json, urllib, json, requests, time, random
from azure.storage.blob import BlockBlobService, PublicAccess, baseblobservice
from azure.common import AzureConflictHttpError
from shutil import copyfile
# Helper scripts
from image_transform import process_image

srcset_file_path = '/../data/srcsets.json'
config_file_path = '/../config/_default/config.yaml'

class AzureImageSizer:
    """ Sets up"""
    def __init__(self, STORAGE_ACCOUNT_NAME, STORAGE_ACCOUNT_KEY, data_map=None, srcsets=None):

        self.STORAGE_ACCOUNT_NAME = STORAGE_ACCOUNT_NAME
        self.STORAGE_ACCOUNT_KEY = STORAGE_ACCOUNT_KEY

        self.SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
        # If not provided, load from file
        if data_map is None:
            # This is brittle
            with open(self.SCRIPT_DIR + config_file_path) as f:
                self.data_map = yaml.safe_load(f)
        else:
            self.data_map = data_map

        if srcsets is None:
            # This is brittle
            with open(self.SCRIPT_DIR + srcset_file_path) as f:
                self.srcsets = json.load(f)
        else:
            self.srcsets = srcsets
    
        # Class wide configs
        self.IMAGE_RESIZE_WIDTHS=self.data_map["params"]["img_sizes"]
        self.SRCSET_UPDATE_NEEDED = False

        # Setup temp dir
        self.temp_dir = f'{os.path.dirname(os.path.abspath(__file__))}/temp'
        if not os.path.exists(self.temp_dir):
            os.makedirs(self.temp_dir)

        # Global regex
        self.already_processed = re.compile(r'.*_([0-9]+)w\.[a-zA-Z]+$')
        self.supported_image = re.compile(r'.*\.(jpg|jpeg|gif|png|webp|jp2)$')

    """Given a storage account, loop through images on the account and pass them to the image processor"""
    def process(self):
        # Connect to our storage service
        block_blob_service = BlockBlobService(account_name=self.STORAGE_ACCOUNT_NAME, account_key=self.STORAGE_ACCOUNT_KEY)

        for container in block_blob_service.list_containers():
            # skip special containers
            if container.name.startswith("azure") or container.name.startswith("$"):
                continue

            print(f"Inspecting container {container.name}...")
            
            # Build a cache for processing this container
            blob_reference = self.build_blob_cache(block_blob_service, container.name)

            # Resize images from their blob counterparts
            new_srcsets = {}
            for blob, orig_width in blob_reference.items():
                new_srcsets = {**new_srcsets, **self.process_blob(block_blob_service, blob, container.name, blob_reference, orig_width)}

            # Update tracker file
            if len(new_srcsets) > 0:
                self.update_tracker_file(block_blob_service, new_srcsets)

        print("Done adding sizes!")
        return


    def build_blob_cache(self, block_blob_service, container_name):
        """Given a blob account and container, build a cache of files to process"""
        """ Get all blob names - this is useful to check if image already has sizes
        Get the size of the image if it's there - don't need to size up images """
        blob_reference = {}
        for blob in block_blob_service.list_blobs(container_name):
            # If orig image and already tracked
            if f'{container_name}/{blob.name}' in self.srcsets:
                continue
            match = re.search(self.already_processed, blob.name)
            # If sized image of orig image already tracked
            if match and int(match.group(1)) in self.srcsets.get(container_name + '/' + blob.name.replace(f'_{match.group(1)}w', ''), []):
                    continue
            # Filter out invalid images
            if self.supported_image.match(blob.name):
                if blob.metadata is None:
                    blob_reference[blob.name] = None
                else:
                    blob_reference[blob.name] = blob.metadata.Get("width", None)

        return blob_reference


    def process_blob(self, block_blob_service, blob_name, container_name, blob_reference, orig_width):
        new_srcsets = {}

        if self.already_processed.match(blob_name):
            return new_srcsets

        temp_image_path = f'{self.temp_dir}/{blob_name}'
        print(f"Processing blob {blob_name}...")

        if not os.path.isfile(temp_image_path):
            if not os.path.exists("/".join(temp_image_path.split("/")[0:-1])):
                os.makedirs("/".join(temp_image_path.split("/")[0:-1]))
            block_blob_service.get_blob_to_path(container_name=container_name, blob_name=blob_name, file_path=temp_image_path)

        ignore_list = []
        for reference in blob_reference:
            is_derived_blob = re.compile(".".join(blob_name.split(".")[0:-1]) + r'_([0-9]+)w\.[a-zA-Z]+$')
            if is_derived_blob.match(reference):
                ignore_list.append(reference)
        try:
            resized_images = process_image(temp_image_path, self.IMAGE_RESIZE_WIDTHS, ignore_list)
        except Exception as ex:
            print(f'ERROR RESIZING IMAGE {container_name}/{blob_name}. {ex}')
            return new_srcsets

        # Upload, track, and clear resized blobs
        new_srcsets[f'{container_name}/{blob_name}'] = {}
        for extension in resized_images:
            new_srcsets[f'{container_name}/{blob_name}'][extension] = []
            for size in resized_images[extension]:
                new_blob_name = f'{".".join(blob_name.split(".")[0:-1])}_{size}w.{extension}'
                temp_resized_path = f'{".".join(temp_image_path.split(".")[0:-1])}_{size}w.{extension}'
                if os.path.isfile(temp_resized_path):
                    block_blob_service.create_blob_from_path(container_name, new_blob_name, temp_resized_path)
                else:
                    print(f'WARNING: {temp_resized_path} was skipped')

                if not f'{container_name}/{blob_name}' in self.srcsets or not extension in self.srcsets[f'{container_name}/{blob_name}'] or not size in self.srcsets[f'{container_name}/{blob_name}'][extension]:
                    new_srcsets[f'{container_name}/{blob_name}'][extension].append(size)

                if os.path.isfile(temp_resized_path):
                    os.remove(temp_resized_path)
        # Remove orig file
        if os.path.isfile(temp_image_path):
            os.remove(temp_image_path)

        return new_srcsets


    def update_tracker_file(self, block_blob_service, new_srcsets):
        if not block_blob_service.exists("$web", "srcsets.json"):
            block_blob_service.create_blob_from_text("$web", "srcsets.json", "{}")

        def write_srcset():
            try:
                lease_id = block_blob_service.acquire_blob_lease("$web", "srcsets.json", 60)
                srcsets = block_blob_service.get_blob_to_text("$web", "srcsets.json")

                write_needed = False

                srcsets_json = json.loads(srcsets.content)
                for image in new_srcsets:
                    for extension in new_srcsets[image]:
                        if image in srcsets_json:
                            new_value = list(set(new_srcsets[image][extension]) | set(srcsets_json[image].get(extension, [])))
                            if new_value == srcsets_json[image][extension]:
                                continue
                            srcsets_json[image][extension] = list(set(new_srcsets[image][extension]) | set(srcsets_json[image].get(extension, [])))
                        else:
                            srcsets_json[image] = new_srcsets[image]
                        write_needed = True

                if write_needed:
                    block_blob_service.create_blob_from_text("$web", "srcsets.json", json.dumps(srcsets_json), lease_id=lease_id)
                block_blob_service.release_blob_lease("$web", "srcsets.json", lease_id)

                # This is brittle
                if write_needed:
                    with open(self.SCRIPT_DIR + srcset_file_path, "w") as f:
                        json.dump(srcsets_json, f)
                return True
            except AzureConflictHttpError:
                return False;

        try:
            while True:
                if (write_srcset()):
                    break
                time.sleep (random.randint(1,3056) / 1000.0);
        except Exception as ex:
            print(f'Exception encountered writing back to srcset. {ex}')

from dotenv import load_dotenv, find_dotenv
load_dotenv(find_dotenv())

BLOB_ACCOUNT_NAME = os.getenv("BLOB_ACCOUNT_NAME")
BLOB_ACCOUNT_KEY = os.getenv("BLOB_ACCOUNT_KEY")

sizer = AzureImageSizer(BLOB_ACCOUNT_NAME, BLOB_ACCOUNT_KEY)
sizer.process()