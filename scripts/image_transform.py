import PIL
from PIL import Image

def process_image(image_path, image_sizes, ignore_list = []):
    """
    From an input image path, orchestrate the resizing, converting, and persisting.
    """
    resized_images = {}

    # resize for sizes
    for size in image_sizes:
        # Add standard format sizes
        parts = image_path.split('.')
        resized_path = f'{".".join(parts[:-1])}_{size}w.{parts[-1]}'
        webp3_path = f'{".".join(parts[:-1])}_{size}w.webp'
        # jpeg2k_path = f'{".".join(parts[:-1])}_{size}w.jp2'

        # Skip if sizes already there
        do_work = True
        if any(resized_path.endswith(item) for item in ignore_list) and any(webp3_path.endswith(item) for item in ignore_list):
            do_work = False

        if do_work:
            img = Image.open(image_path)
            if img.width <= size:
                continue
            wpercent = (size / float(img.size[0]))
            hsize = int((float(img.size[1]) * float(wpercent)))
            img = img.resize((size, hsize), PIL.Image.ANTIALIAS)
        
            # This should always succeed
            img.save(resized_path)
        
        if parts[-1] in resized_images:
            resized_images[parts[-1]].append(size)
        else:
            resized_images[parts[-1]] = [size]

        try:
            if do_work:
                img.save(webp3_path, 'webp', quality = 70)
            if "webp" in resized_images:
                resized_images["webp"].append(size)
            else:
                resized_images["webp"] = [size]
        except Exception as ex:
            print(f'Error converting {resized_path} to webp. {ex}')
        """try:
            img.save(jpeg2k_path, 'JPEG2000', quality_mode='dB', quality_layers=[38])
            if "jp2" in resized_images:
                resized_images["jp2"].append(size)
            else:
                resized_images["jp2"] = [size]
        except Exception as ex:
            print(f'Error converting {resized_path} to jpeg2k. {ex}')"""

    return resized_images