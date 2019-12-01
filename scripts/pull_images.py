from azure.storage.blob import BlockBlobService, PublicAccess
import os, pathlib, re, json

def run(BLOB_ACCOUNT_NAME, BLOB_ACCOUNT_KEY):
    """
    Pull any new images from Azure Blob to local (git ignore) folder.
    Keeps structure hosted in Azure so URL base can be replaced.
    """

    SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
    srcset_file_path = SCRIPT_DIR + '/../data/srcsets.json'

    # Script helper validators - let's not do bad things
    if BLOB_ACCOUNT_NAME == "" or BLOB_ACCOUNT_KEY == "":
        raise Exception("Must have BLOB_ACCOUNT_NAME and BLOB_ACCOUNT_KEY env variables set!")
    block_blob_service = BlockBlobService(account_name=BLOB_ACCOUNT_NAME, account_key=BLOB_ACCOUNT_KEY)

    def download_blob_if_needed(container_name, blob_name, file_path, blob_length=-1, block_blob_service=block_blob_service):
        # TODO - make a more sound check here
        if os.path.isfile(file_path) and (os.path.getsize(file_path) == blob_length or blob_length == -1):
            return

        # Download blob
        print(f'Downloading {blob_name} from {container_name} to {file_path}...')
        os.makedirs(os.path.dirname(file_path), exist_ok=True)
        block_blob_service.get_blob_to_path(container_name=container_name, blob_name=blob_name, file_path=file_path)


    try:
        supported_image = re.compile(r'.*\.(jpg|jpeg|gif|png)$')
        already_processed = re.compile(r'.*_[0-9]+w\.[a-zA-Z]+$')

        # Load srcset 
        with open(srcset_file_path) as f:
            srcsets = json.load(f)
        QUICK_PROCESS = True

        if QUICK_PROCESS:
            for blob_path in srcsets:
                container = blob_path.split("/")[0]
                blob_name = '/'.join(blob_path.split("/")[1:])
                if  container == "img":
                    file_path = f'static/img/{blob_name}'
                else:
                    file_path = f'content/{container}/{blob_name}'

                download_blob_if_needed(container, blob_name, file_path)
        else :
            containers = block_blob_service.list_containers()
            for container in containers:
                if container.name.startswith("azure") or container.name.startswith("$"):
                    continue
                print (f'Inspecting container {container.name}...')
                for blob in block_blob_service.list_blobs(container_name=container.name):
                    if  container == "img":
                        file_path = f'static/img/{blob.name}'
                    else:
                        file_path = f'content/{container}/{blob.name}'

                    # skip already processed or not supported blobs
                    if already_processed.match(blob.name) or not supported_image.match(blob.name):
                        continue

                    download_blob_if_needed(container, blob.name, file_path, blob.properties.content_length)

    except Exception as err:
        raise err

    print("Done pulling images!!")

from dotenv import load_dotenv, find_dotenv
load_dotenv(find_dotenv())

BLOB_ACCOUNT_NAME = os.getenv("BLOB_ACCOUNT_NAME")
BLOB_ACCOUNT_KEY = os.getenv("BLOB_ACCOUNT_KEY")
run(BLOB_ACCOUNT_NAME, BLOB_ACCOUNT_KEY)