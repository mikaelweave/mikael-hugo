import os, pathlib, shutil, re, yaml, glob
from sortedcontainers import SortedList

from azure.storage.blob import BlockBlobService, PublicAccess, baseblobservice
import os, pathlib, re



supported_image = re.compile(r'.*\.(jpg|jpeg|gif|png|JPG|JPEG|GIF|PNG)$')
already_processed = re.compile(r'(.*)_[0-9]+w\.([a-zA-Z]+)$')
img_list = SortedList()

def run(STORAGE_ACCOUNT_NAME, STORAGE_ACCOUNT_KEY):

    # Get List of all images
    for subdir, dirs, files in os.walk("public/"):
        for file in files:
            if file.endswith(".html"):
                imgs = []
                with open(os.path.join(subdir, file), 'r+') as content_file:
                    content = content_file.read()

                    pattern = re.compile(r'(src|srcset)=(\'|\")([^\'\"]+)')
                    all_match = set(re.findall(pattern, content))

                    for match in all_match:
                        img_ref = match[2].replace("https://katyweavercdn3.blob.core.windows.net/", "").replace("%20", " ")
                        if not supported_image.match(img_ref):
                            continue
                        match = already_processed.match(img_ref)
                        if match:
                            img_ref = match.group(1) + '.' + match.group(2)
                        if img_list.count(img_ref) == 0:
                            img_list.add(img_ref)

    # Find local images to delete
    def remove_imgs_in_local(directory, rel_dir):
        print(f"Processing local {directory}...")
        last_dir = ""
        for subdir, dirs, files in os.walk(directory):
            if last_dir != subdir:
                last_dir = subdir
                print(f'Subdirectory {last_dir}...') 

            for file in files:
                if pathlib.Path(file).suffix.lower() in ['.jpg', '.jpeg', '.png', 'gif']:
                    file_path = os.path.realpath(os.path.join(subdir, file))
                    file_reference = f'{subdir}/{file}'

                    normalized_file = file
                    match = already_processed.match(file)
                    if match:
                        normalized_file = match.group(1) + '.' + match.group(2)
                    if img_list.count(f"{subdir.replace('static/', '').replace('content/', '').rstrip('/')}/{normalized_file}") == 0:
                        print(f"Found Blob To Delete...{subdir}/{file}")

    try:
        # Push static images
        remove_imgs_in_local("static/img/", "img")
        # Iterate through content dirs, if they have a single image, process whole dir
        for directory in next(os.walk('content/'))[1]:
            remove_imgs_in_local(os.path.join("content/", directory), directory)
    except Exception as err:
        print(f"Error removing orphaned images from Azure. {err}") 

    # Find cloud images to delete 
    def remove_imgs_in_azure(directory, blob_container_name):
        print(f"Processing Azure {directory}...")
        block_blob_service = BlockBlobService(account_name=STORAGE_ACCOUNT_NAME, account_key=STORAGE_ACCOUNT_KEY)

        blob_reference = {}
        # It's helpful to just have a list of blobs in the container
        for blob in block_blob_service.list_blobs(blob_container_name):
            # skip already processed or not supported blobs
            blob_name = blob.name
            if supported_image.match(blob_name):
                match = already_processed.match(blob_name)
                if match:
                    blob_name = match.group(1) + '.' + match.group(2)
                if img_list.count(f"{blob_container_name}/{blob_name}") == 0:
                    print(f"Found Blob To Delete...{blob_container_name}/{blob.name}")

    try:
        # Push static images
        remove_imgs_in_azure("static/img/", "img")
        # Iterate through content dirs, if they have a single image, process whole dir
        for directory in next(os.walk('content/'))[1]:
            remove_imgs_in_azure(os.path.join("content/", directory), directory)
    except Exception as err:
        print(f"Error removing orphaned images from Azure. {err}") 

    print("Done!")
    return

from dotenv import load_dotenv, find_dotenv
load_dotenv(find_dotenv())

BLOB_ACCOUNT_NAME = os.getenv("BLOB_ACCOUNT_NAME")
BLOB_ACCOUNT_KEY = os.getenv("BLOB_ACCOUNT_KEY")
run(BLOB_ACCOUNT_NAME, BLOB_ACCOUNT_KEY)