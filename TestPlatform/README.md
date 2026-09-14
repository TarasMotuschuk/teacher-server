# TestPlatform

Experimental tools for converting MyTest XML exports into simpler formats.

## Files

- `convert_mytest_xml.py` converts one or more MyTest XML files into JSON.
- `viewer.html` loads a generated JSON file and renders it as a readable test page.

## Usage

```bash
python3 TestPlatform/convert_mytest_xml.py \
  "/path/to/test1.xml" \
  "/path/to/test2.xml" \
  --output-dir TestPlatform/out
```

By default, question images are exported as external `webp` files next to the JSON. Use `--image-mode embedded` to keep images inside JSON, or `--skip-images` to omit them.

Then open `TestPlatform/viewer.html` in a browser and pass a JSON file using the `src` query parameter:

```text
file:///.../TestPlatform/viewer.html?src=out/test1.json
```

If no `src` is provided, the viewer defaults to `out/index.json`.
