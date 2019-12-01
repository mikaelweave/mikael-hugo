from azure.storage.blob import BlockBlobService, PublicAccess, baseblobservice
import os, pathlib, re

def run(STORAGE_ACCOUNT_NAME, STORAGE_ACCOUNT_KEY):
    """
    Pushes images from generated Hugo output public folder to Azure
    """
    # Script helper validators - let's not do bad things
    if STORAGE_ACCOUNT_NAME == "" or STORAGE_ACCOUNT_KEY == "":
        raise Exception("Must have STORAGE_ACCOUNT_NAME and STORAGE_ACCOUNT_KEY env variables set!")

    def push_imgs_in_dir(directory, blob_container_name):
        print(f"Processing {directory}...")
        block_blob_service = BlockBlobService(account_name=STORAGE_ACCOUNT_NAME, account_key=STORAGE_ACCOUNT_KEY)
        block_blob_service.create_container(blob_container_name)
        block_blob_service.set_container_acl(blob_container_name, public_access=PublicAccess.Container)

        # Load srcset 
        #with open(srcset_file_path) as f:
        #    srcsets = json.load(f)
        #QUICK_PROCESS = True

        blob_reference = {}
        # It's helpful to just have a list of blobs in the container
        for blob in block_blob_service.list_blobs(blob_container_name):
            # skip already processed or not supported blobs
            supported_image = re.compile(r'.*\.(jpg|jpeg|gif|png)$')
            already_processed = re.compile(r'.*_[0-9]+w\.[a-zA-Z]+$')
            if not already_processed.match(blob.name) and supported_image.match(blob.name):
                blob_reference[blob.name] = blob.properties.content_length

        last_dir = ""
        for subdir, dirs, files in os.walk(directory):
            if last_dir != subdir:
                last_dir = subdir
                print(f'Subdirectory {last_dir}...') 

            for file in files:
                if pathlib.Path(file).suffix.lower() in ['.jpg', '.jpeg', '.png', 'gif']:
                    file_path = os.path.realpath(os.path.join(subdir, file))
                    blob_name = file_path.replace(f'{os.path.realpath(directory)}/', "")

                    # Skip unchanged files - probably could use a better approach here
                    if blob_name in blob_reference and blob_reference[blob_name] == os.path.getsize(file_path):
                        continue

                    # Write blob and associated metadata
                    print(f'Pushing image {file_path}...')
                    block_blob_service.create_blob_from_path(container_name=blob_container_name, blob_name=blob_name, file_path=file_path)
    try:
        # Push static images
        push_imgs_in_dir("static/img/", "img")
        # Iterate through content dirs, if they have a single image, process whole dir
        for directory in next(os.walk('content/'))[1]:
            push_imgs_in_dir(os.path.join("content/", directory), directory)
    except Exception as err:
        print(f"Error pushing to Azure. {err}") 

    print("Done pushing images!")
    return

from dotenv import load_dotenv, find_dotenv
load_dotenv(find_dotenv())

BLOB_ACCOUNT_NAME = os.getenv("BLOB_ACCOUNT_NAME")
BLOB_ACCOUNT_KEY = os.getenv("BLOB_ACCOUNT_KEY")
run(BLOB_ACCOUNT_NAME, BLOB_ACCOUNT_KEY)