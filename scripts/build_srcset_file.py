import re, yaml, os, json
from azure.storage.blob import BlockBlobService, PublicAccess, baseblobservice


def run(STORAGE_ACCOUNT_NAME, STORAGE_ACCOUNT_KEY):
    """
    Builds file of srcsets to help with Hugo generation
    """

    # Script helper validators - let's not do bad things
    if STORAGE_ACCOUNT_NAME == "" or STORAGE_ACCOUNT_KEY == "":
        raise Exception("Must have STORAGE_ACCOUNT_NAME and STORAGE_ACCOUNT_KEY env variables set!")

    file_update_needed = False
    srcsets = {}

    block_blob_service = BlockBlobService(account_name=STORAGE_ACCOUNT_NAME, account_key=STORAGE_ACCOUNT_KEY)

    # lock and download srcsets file
    lease_id = block_blob_service.acquire_blob_lease("$web", "srcsets.json", -1)
    srcsets = json.loads(block_blob_service.get_blob_to_text("$web", "srcsets.json").content)

    # Shared in all loop iterations
    supported_image = re.compile(r'.*\.(jpg|jpeg|gif|png|webp)$')
    already_processed = re.compile(r'.*_([0-9]+)w\.[a-zA-Z]+$')

    for container in block_blob_service.list_containers():
        # skip special containers
        if container.name.startswith("azure") or container.name.startswith("$"):
            continue

        print(f"Processing container {container.name}...")

        blob_reference = {}

        # It's helpful to just have a list of blobs in the container
        for blob in block_blob_service.list_blobs(container.name):
            if f'{container.name}/{blob.name}' in srcsets:
                continue
            match = re.search(already_processed, blob.name)
            if match:
                if int(match.group(1)) in srcsets.get(container.name + '/' + blob.name.replace(f'_{match.group(1)}w', ''), []):
                    continue
            # Filter out invalid images
            if supported_image.match(blob.name):
                if blob.metadata is None:
                    blob_reference[blob.name] = None
                else:
                    blob_reference[blob.name] = blob.metadata.Get("width", None)

        i = 0
        for blob in blob_reference:
            i = i + 1
            if i % 1000 == 0:
                print (f'Processing blob {i} of {len(blob_reference)}...')

            sizes = {}
            resize_filter = re.compile(re.escape('.'.join(blob.split('.')[:-1])) + r'.*_([0-9]+)w\.([a-zA-Z]+)$')
            if already_processed.match(blob):
                continue
            for blobname in list(filter(resize_filter.match, blob_reference)):
                match = re.search(resize_filter, blobname)
                if match:
                    if match.group(2) in sizes:
                        sizes[match.group(2)].append(int(match.group(1)))
                    else:
                        sizes[match.group(2)] = [int(match.group(1))]

            for extension in sizes:
                # Check truth vs file - update in memory object and flag for save if change detected
                sizes[extension].sort()
                if len(sizes[extension]) > 0 and (blob not in srcsets or srcsets.get(blob, {}).get(extension, None) != sizes[extension]):
                    if not f'{container.name}/{blob}' in srcsets:
                        srcsets[f'{container.name}/{blob}'] = {}
                    srcsets[f'{container.name}/{blob}'][extension] = sizes[extension]
                    file_update_needed = True

    # Only update file if update is actually needed
    if file_update_needed:
        block_blob_service.create_blob_from_text("$web", "srcsets.json", json.dumps(srcsets), lease_id=lease_id)
    block_blob_service.release_blob_lease("$web", "srcsets.json", lease_id)

    print("Done generating srcset file!")

from dotenv import load_dotenv, find_dotenv
load_dotenv(find_dotenv())

BLOB_ACCOUNT_NAME = os.getenv("BLOB_ACCOUNT_NAME")
BLOB_ACCOUNT_KEY = os.getenv("BLOB_ACCOUNT_KEY")
run(BLOB_ACCOUNT_NAME, BLOB_ACCOUNT_KEY)