#!/usr/bin/env python3

from __future__ import annotations

import argparse
import base64
import json
import logging
from io import BytesIO
import re
import sys
import time
import traceback
from collections import Counter
from pathlib import Path
import xml.etree.ElementTree as ET

from PIL import Image


def slugify(value: str) -> str:
    slug = re.sub(r"[^0-9A-Za-zА-Яа-яІіЇїЄєҐґ_-]+", "-", value.strip(), flags=re.UNICODE)
    slug = re.sub(r"-{2,}", "-", slug).strip("-")
    return slug or "test"


def text_value(parent: ET.Element | None, tag: str) -> str | None:
    if parent is None:
        return None
    element = parent.find(tag)
    if element is None or element.text is None:
        return None
    return element.text.strip()


def bool_value(value: str | None) -> bool | None:
    if value is None:
        return None
    lowered = value.strip().lower()
    if lowered == "true":
        return True
    if lowered == "false":
        return False
    return None


def parse_formatted_block(parent: ET.Element | None) -> dict[str, str | None]:
    return {
        "plainText": text_value(parent, "PlainText"),
        "plainText2": text_value(parent, "PlainText2"),
        "formattedText": text_value(parent, "FormattedText"),
        "rtf": text_value(parent, "RTF"),
    }


def primary_text(block: dict[str, str | None]) -> str:
    return block.get("plainText") or block.get("plainText2") or ""


def parse_variant(variant: ET.Element) -> dict[str, object]:
    block = parse_formatted_block(variant)
    correct = variant.attrib.get("CorrectAnswer")
    is_correct = bool_value(correct)

    payload: dict[str, object] = {
        "text": primary_text(block),
        "content": block,
        "correctAnswer": correct,
        "isCorrect": is_correct,
    }
    if correct and correct.isdigit():
        payload["correctOrder"] = int(correct)
    return payload


def save_external_image(
    image_bytes: bytes,
    image_name: str,
    test_slug: str,
    output_dir: Path,
    image_format: str,
) -> dict[str, object]:
    image_dir = output_dir / f"{test_slug}_assets"
    image_dir.mkdir(parents=True, exist_ok=True)

    source_stem = Path(image_name).stem if image_name else "image"
    source_stem = slugify(source_stem)

    if image_format == "bmp":
        target_name = f"{source_stem}.bmp"
        target_path = image_dir / target_name
        target_path.write_bytes(image_bytes)
    elif image_format == "webp":
        target_name = f"{source_stem}.webp"
        target_path = image_dir / target_name
        with Image.open(BytesIO(image_bytes)) as img:
            converted = img.convert("RGBA") if img.mode not in ("RGB", "RGBA") else img
            converted.save(target_path, format="WEBP", quality=90, method=6)
    else:
        raise ValueError(f"Unsupported image format: {image_format}")

    return {
        "fileName": image_name,
        "format": image_format,
        "path": target_path.relative_to(output_dir).as_posix(),
        "omitted": False,
    }


def parse_image(
    element: ET.Element | None,
    include_images: bool,
    image_mode: str,
    test_slug: str,
    output_dir: Path,
) -> dict[str, object] | None:
    if element is None or not (element.text or "").strip():
        return None
    image_name = element.attrib.get("FileName", "")
    if not include_images:
        return {
            "fileName": image_name,
            "format": "bmp",
            "omitted": True,
        }
    raw_base64 = element.text.strip()
    normalized_base64 = normalize_question_image(raw_base64)
    image_bytes = base64.b64decode(normalized_base64)

    if image_mode in {"bmp", "webp"}:
        return save_external_image(image_bytes, image_name, test_slug, output_dir, image_mode)

    return {
        "fileName": image_name,
        "format": "bmp",
        "base64": normalized_base64,
        "omitted": False,
    }


def normalize_question_image(raw_base64: str) -> str:
    try:
        decoded = base64.b64decode(raw_base64)
        text = decoded.decode("utf-8")
        repaired_bytes = bytearray()
        for char in text:
            codepoint = ord(char)
            if codepoint <= 0xFF:
                repaired_bytes.append(codepoint)
            else:
                repaired_bytes.extend(char.encode("cp1251"))
        repaired = bytes(repaired_bytes)
        if repaired.startswith(b"BM"):
            return base64.b64encode(repaired).decode("ascii")
    except Exception:
        pass
    return raw_base64


def parse_regions(task: ET.Element) -> list[str]:
    return [
        (region.text or "").strip()
        for region in task.findall("./Regions/Region")
        if (region.text or "").strip()
    ]


def parse_task(
    task: ET.Element,
    index: int,
    include_images: bool,
    image_mode: str,
    test_slug: str,
    output_dir: Path,
) -> dict[str, object]:
    question_block = parse_formatted_block(task.find("QuestionText"))
    options = task.find("Options")
    is_allow_random = bool_value(text_value(options, "IsAllowRandom"))

    return {
        "index": index,
        "type": task.attrib.get("Type", ""),
        "score": int(task.attrib.get("Score", "0") or 0),
        "question": primary_text(question_block),
        "questionContent": question_block,
        "isAllowRandom": is_allow_random,
        "variants": [parse_variant(variant) for variant in task.findall("./Variants/VariantText")],
        "image": parse_image(task.find("QuestionImage"), include_images, image_mode, test_slug, output_dir),
        "regions": parse_regions(task),
    }


def parse_group(
    group: ET.Element,
    index: int,
    include_images: bool,
    image_mode: str,
    test_slug: str,
    output_dir: Path,
) -> dict[str, object]:
    description = parse_formatted_block(group.find("Description"))
    tasks = [
        parse_task(task, task_index, include_images, image_mode, test_slug, output_dir)
        for task_index, task in enumerate(group.findall("./Tasks/Task"), start=1)
    ]
    return {
        "index": index,
        "title": text_value(group, "Title") or f"Group {index}",
        "description": primary_text(description),
        "descriptionContent": description,
        "countLimit": group.attrib.get("CountLimit"),
        "tasks": tasks,
    }


def parse_mark_levels(test_options: ET.Element | None) -> list[dict[str, int]]:
    if test_options is None:
        return []

    levels: list[dict[str, int]] = []
    for level in test_options.findall("./MarkLevel/Level"):
        value = (level.text or "").strip()
        mark = level.attrib.get("Mark")
        if value.isdigit() and mark and mark.isdigit():
            levels.append({"mark": int(mark), "threshold": int(value)})
    return levels


def parse_test(xml_path: Path, include_images: bool, image_mode: str, output_dir: Path) -> dict[str, object]:
    root = ET.parse(xml_path).getroot()
    test_options = root.find("TestOptions")
    description = parse_formatted_block(test_options.find("Description") if test_options is not None else None)
    groups = root.findall("./Groups/Group")
    title = text_value(test_options, "Title") if test_options is not None else xml_path.stem
    test_slug = slugify(title if isinstance(title, str) else xml_path.stem)

    return {
        "sourceFile": str(xml_path),
        "format": "MyTestX XML",
        "version": text_value(root, "Version"),
        "saveDate": text_value(root, "SaveDate"),
        "title": title,
        "slug": test_slug,
        "description": primary_text(description),
        "descriptionContent": description,
        "author": text_value(test_options, "Author"),
        "authorEmail": text_value(test_options, "AuthorEmail"),
        "testUid": text_value(test_options, "TestUID"),
        "questionFormulization": text_value(test_options, "QuestionFormulization"),
        "isOrderTaskRandom": bool_value(text_value(test_options, "IsOrderTaskRandom")),
        "isOrderVariantRandom": bool_value(text_value(test_options, "IsOrderVariantRandom")),
        "imageMode": image_mode if include_images else "omitted",
        "markLevels": parse_mark_levels(test_options),
        "groups": [
            parse_group(group, index, include_images, image_mode, test_slug, output_dir)
            for index, group in enumerate(groups, start=1)
        ],
    }


def build_summary(parsed: dict[str, object]) -> dict[str, int]:
    groups = parsed.get("groups", [])
    if not isinstance(groups, list):
        groups = []

    tasks = 0
    images = 0
    variants = 0
    for group in groups:
        if not isinstance(group, dict):
            continue
        group_tasks = group.get("tasks", [])
        if not isinstance(group_tasks, list):
            continue
        tasks += len(group_tasks)
        for task in group_tasks:
            if not isinstance(task, dict):
                continue
            if task.get("image"):
                images += 1
            task_variants = task.get("variants", [])
            if isinstance(task_variants, list):
                variants += len(task_variants)

    return {
        "groups": len(groups),
        "tasks": tasks,
        "variants": variants,
        "images": images,
    }


def configure_logging(log_path: Path) -> logging.Logger:
    log_path.parent.mkdir(parents=True, exist_ok=True)
    logger = logging.getLogger("convert_mytest_xml")
    logger.setLevel(logging.INFO)
    logger.handlers.clear()

    formatter = logging.Formatter("%(asctime)s %(levelname)s %(message)s")

    file_handler = logging.FileHandler(log_path, encoding="utf-8")
    file_handler.setFormatter(formatter)
    logger.addHandler(file_handler)

    stream_handler = logging.StreamHandler(sys.stdout)
    stream_handler.setFormatter(formatter)
    logger.addHandler(stream_handler)

    return logger


def scan_xml_file(xml_path: Path) -> dict[str, object]:
    counts: Counter[str] = Counter()
    image_lengths: list[int] = []
    for _event, elem in ET.iterparse(xml_path, events=("end",)):
        counts[elem.tag] += 1
        if elem.tag == "QuestionImage" and elem.text:
            image_lengths.append(len(elem.text.strip()))
        elem.clear()

    return {
        "tasks": counts["Task"],
        "groups": counts["Group"],
        "questionImages": counts["QuestionImage"],
        "variants": counts["VariantText"],
        "imageCharsTotal": sum(image_lengths),
        "largestImageChars": max(image_lengths) if image_lengths else 0,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Convert MyTest XML exports to JSON.")
    parser.add_argument("xml_files", nargs="+", help="Source MyTest XML files")
    parser.add_argument("--output-dir", default="TestPlatform/out", help="Directory for generated JSON files")
    parser.add_argument("--log-file", default=None, help="Optional log file path")
    parser.add_argument(
        "--skip-images",
        action="store_true",
        help="Do not embed QuestionImage base64 into output JSON",
    )
    parser.add_argument(
        "--image-mode",
        choices=["embedded", "bmp", "webp"],
        default="webp",
        help="How to store question images in output JSON and assets",
    )
    args = parser.parse_args()

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    log_path = Path(args.log_file) if args.log_file else output_dir / "convert.log"
    logger = configure_logging(log_path)

    index: list[dict[str, str]] = []
    errors = 0

    logger.info("Starting conversion for %s file(s)", len(args.xml_files))

    for xml_name in args.xml_files:
        xml_path = Path(xml_name).expanduser().resolve()
        started_at = time.perf_counter()
        logger.info("Processing %s", xml_path)
        try:
            file_size = xml_path.stat().st_size
            scan_started_at = time.perf_counter()
            scan = scan_xml_file(xml_path)
            scan_elapsed = time.perf_counter() - scan_started_at
            logger.info(
                "Preflight %s | size=%s bytes groups=%s tasks=%s variants=%s questionImages=%s imageChars=%s largestImageChars=%s | %.2fs",
                xml_path.name,
                file_size,
                scan["groups"],
                scan["tasks"],
                scan["variants"],
                scan["questionImages"],
                scan["imageCharsTotal"],
                scan["largestImageChars"],
                scan_elapsed,
            )
            if args.skip_images:
                logger.info("Embedded images will be omitted from JSON for %s", xml_path.name)
            elif args.image_mode != "embedded":
                logger.info("Question images will be exported as external %s files for %s", args.image_mode, xml_path.name)

            parsed = parse_test(
                xml_path,
                include_images=not args.skip_images,
                image_mode=args.image_mode,
                output_dir=output_dir,
            )
            output_name = f"{slugify(parsed['title'] if isinstance(parsed['title'], str) else xml_path.stem)}.json"
            output_path = output_dir / output_name
            output_path.write_text(json.dumps(parsed, ensure_ascii=False, indent=2), encoding="utf-8")
            index.append(
                {
                    "title": str(parsed.get("title") or xml_path.stem),
                    "jsonFile": output_name,
                    "sourceFile": str(xml_path),
                }
            )
            summary = build_summary(parsed)
            elapsed = time.perf_counter() - started_at
            logger.info(
                "Wrote %s | groups=%s tasks=%s variants=%s images=%s | %.2fs",
                output_path,
                summary["groups"],
                summary["tasks"],
                summary["variants"],
                summary["images"],
                elapsed,
            )
        except Exception as exc:
            errors += 1
            elapsed = time.perf_counter() - started_at
            logger.error("Failed to convert %s after %.2fs: %s", xml_path, elapsed, exc)
            logger.error(traceback.format_exc().rstrip())

    index_path = output_dir / "index.json"
    index_path.write_text(
        json.dumps({"tests": index}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    logger.info("Wrote %s", index_path)
    logger.info("Finished: success=%s failed=%s log=%s", len(index), errors, log_path.resolve())


if __name__ == "__main__":
    main()
