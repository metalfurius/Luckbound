"""Small standard-library validator for the JSON Schema keywords used by F07."""
from __future__ import annotations

import json
import math
import re
from pathlib import Path
from typing import Any


class JsonSchemaValidationError(ValueError):
    pass


def _matches_type(value: Any, expected: str) -> bool:
    if expected == "object":
        return isinstance(value, dict)
    if expected == "array":
        return isinstance(value, list)
    if expected == "string":
        return isinstance(value, str)
    if expected == "boolean":
        return isinstance(value, bool)
    if expected == "integer":
        return type(value) is int
    if expected == "number":
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    raise JsonSchemaValidationError(f"Unsupported schema type {expected!r}")


def _validate(value: Any, schema: dict[str, Any], path: str) -> None:
    expected_type = schema.get("type")
    if expected_type is not None and not _matches_type(value, expected_type):
        raise JsonSchemaValidationError(f"{path}: expected {expected_type}")

    if "const" in schema and value != schema["const"]:
        raise JsonSchemaValidationError(f"{path}: must equal {schema['const']!r}")
    if "enum" in schema and value not in schema["enum"]:
        raise JsonSchemaValidationError(f"{path}: value {value!r} is outside the allowed enum")

    if isinstance(value, dict):
        for field in schema.get("required", []):
            if field not in value:
                raise JsonSchemaValidationError(f"{path}: missing required field {field!r}")
        properties = schema.get("properties", {})
        if schema.get("additionalProperties") is False:
            extra = set(value) - set(properties)
            if extra:
                raise JsonSchemaValidationError(f"{path}: unknown field(s) {sorted(extra)}")
        for field, field_value in value.items():
            if field in properties:
                _validate(field_value, properties[field], f"{path}.{field}")

    if isinstance(value, list):
        if len(value) < schema.get("minItems", 0):
            raise JsonSchemaValidationError(f"{path}: needs at least {schema['minItems']} items")
        if len(value) > schema.get("maxItems", math.inf):
            raise JsonSchemaValidationError(f"{path}: allows at most {schema['maxItems']} items")
        item_schema = schema.get("items")
        if item_schema is not None:
            for index, item in enumerate(value):
                _validate(item, item_schema, f"{path}[{index}]")

    if isinstance(value, str):
        if len(value) < schema.get("minLength", 0):
            raise JsonSchemaValidationError(f"{path}: string is too short")
        if "pattern" in schema and re.search(schema["pattern"], value) is None:
            raise JsonSchemaValidationError(f"{path}: value {value!r} does not match the required pattern")

    if isinstance(value, (int, float)) and not isinstance(value, bool):
        if not math.isfinite(float(value)):
            raise JsonSchemaValidationError(f"{path}: number must be finite")
        if "minimum" in schema and value < schema["minimum"]:
            raise JsonSchemaValidationError(f"{path}: must be at least {schema['minimum']}")
        if "maximum" in schema and value > schema["maximum"]:
            raise JsonSchemaValidationError(f"{path}: must be at most {schema['maximum']}")
        if "exclusiveMinimum" in schema and value <= schema["exclusiveMinimum"]:
            raise JsonSchemaValidationError(f"{path}: must be greater than {schema['exclusiveMinimum']}")
        if "multipleOf" in schema and not math.isclose(value / schema["multipleOf"], round(value / schema["multipleOf"]), abs_tol=1e-9):
            raise JsonSchemaValidationError(f"{path}: must be a multiple of {schema['multipleOf']}")


def validate_instance(instance: Any, schema_path: Path) -> None:
    try:
        schema = json.loads(schema_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise JsonSchemaValidationError(f"Cannot load score schema: {error}") from error
    if not isinstance(schema, dict):
        raise JsonSchemaValidationError("Score schema root must be an object")
    _validate(instance, schema, "$score")
