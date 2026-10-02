#!/usr/bin/env python3
"""Dependency-free verifier for the EasyNoty Protocol v1 contract."""

from __future__ import annotations

import base64
import hashlib
import hmac
import json
import math
import re
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
SCHEMA_DIR = ROOT / "schemas"
VALID_DIR = ROOT / "fixtures" / "valid"
INVALID_DIR = ROOT / "fixtures" / "invalid"
FIXTURE_SECRET = bytes(range(32))


class ValidationError(Exception):
    pass


def canonical_json(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def sha256_id(data: bytes) -> str:
    return "sha256:" + hashlib.sha256(data).hexdigest()


def b64url_decode_no_padding(value: str) -> bytes:
    return base64.urlsafe_b64decode(value + "=" * (-len(value) % 4))


class SchemaValidator:
    def __init__(self, schema_dir: Path) -> None:
        self.schemas = {
            path.name: json.loads(path.read_text(encoding="utf-8"))
            for path in sorted(schema_dir.glob("*.schema.json"))
        }
        if not self.schemas:
            raise RuntimeError("No schemas found")

    def schema_ref(self, ref: str, current_file: str) -> tuple[dict[str, Any], str]:
        file_part, _, fragment = ref.partition("#")
        target_file = file_part or current_file
        if target_file not in self.schemas:
            raise ValidationError(f"unknown schema reference {target_file!r}")
        target: Any = self.schemas[target_file]
        if fragment:
            if not fragment.startswith("/"):
                raise ValidationError(f"unsupported JSON pointer #{fragment}")
            for raw_part in fragment[1:].split("/"):
                part = raw_part.replace("~1", "/").replace("~0", "~")
                if not isinstance(target, dict) or part not in target:
                    raise ValidationError(f"unresolved schema reference {ref!r}")
                target = target[part]
        if not isinstance(target, dict):
            raise ValidationError(f"schema reference {ref!r} is not an object")
        return target, target_file

    def validate(self, instance: Any, schema: dict[str, Any], current_file: str, path: str = "$") -> None:
        if "$ref" in schema:
            target, target_file = self.schema_ref(schema["$ref"], current_file)
            self.validate(instance, target, target_file, path)
            return

        if "oneOf" in schema:
            successes = 0
            errors: list[str] = []
            for candidate in schema["oneOf"]:
                try:
                    self.validate(instance, candidate, current_file, path)
                    successes += 1
                except ValidationError as error:
                    errors.append(str(error))
            if successes != 1:
                raise ValidationError(f"{path}: expected exactly one oneOf match, got {successes}; {errors[:2]}")

        if "allOf" in schema:
            for candidate in schema["allOf"]:
                self.validate(instance, candidate, current_file, path)

        if "const" in schema and instance != schema["const"]:
            raise ValidationError(f"{path}: expected constant {schema['const']!r}")
        if "enum" in schema and instance not in schema["enum"]:
            raise ValidationError(f"{path}: {instance!r} is not in enum")

        expected_type = schema.get("type")
        if expected_type is not None:
            allowed = expected_type if isinstance(expected_type, list) else [expected_type]
            if not any(self._is_type(instance, item) for item in allowed):
                raise ValidationError(f"{path}: expected type {allowed}, got {type(instance).__name__}")

        if isinstance(instance, dict):
            required = schema.get("required", [])
            for key in required:
                if key not in instance:
                    raise ValidationError(f"{path}: missing required property {key!r}")
            properties = schema.get("properties", {})
            if schema.get("additionalProperties") is False:
                extras = set(instance) - set(properties)
                if extras:
                    raise ValidationError(f"{path}: unexpected properties {sorted(extras)}")
            for key, child in instance.items():
                if key in properties:
                    self.validate(child, properties[key], current_file, f"{path}.{key}")

        if isinstance(instance, list):
            if len(instance) < schema.get("minItems", 0):
                raise ValidationError(f"{path}: too few items")
            if "maxItems" in schema and len(instance) > schema["maxItems"]:
                raise ValidationError(f"{path}: too many items")
            if schema.get("uniqueItems"):
                keys = [canonical_json(item) for item in instance]
                if len(keys) != len(set(keys)):
                    raise ValidationError(f"{path}: items must be unique")
            if "items" in schema:
                for index, item in enumerate(instance):
                    self.validate(item, schema["items"], current_file, f"{path}[{index}]")

        if isinstance(instance, str):
            if len(instance) < schema.get("minLength", 0):
                raise ValidationError(f"{path}: string is too short")
            if "maxLength" in schema and len(instance) > schema["maxLength"]:
                raise ValidationError(f"{path}: string is too long")
            if "pattern" in schema and re.fullmatch(schema["pattern"], instance) is None:
                raise ValidationError(f"{path}: string does not match {schema['pattern']!r}")

        if isinstance(instance, (int, float)) and not isinstance(instance, bool):
            if not math.isfinite(instance):
                raise ValidationError(f"{path}: number must be finite")
            if "minimum" in schema and instance < schema["minimum"]:
                raise ValidationError(f"{path}: value is below minimum")
            if "maximum" in schema and instance > schema["maximum"]:
                raise ValidationError(f"{path}: value is above maximum")

    @staticmethod
    def _is_type(instance: Any, expected: str) -> bool:
        checks = {
            "null": instance is None,
            "boolean": isinstance(instance, bool),
            "integer": isinstance(instance, int) and not isinstance(instance, bool),
            "number": isinstance(instance, (int, float)) and not isinstance(instance, bool),
            "string": isinstance(instance, str),
            "array": isinstance(instance, list),
            "object": isinstance(instance, dict),
        }
        if expected not in checks:
            raise ValidationError(f"unsupported schema type {expected!r}")
        return checks[expected]

    def verify_references(self) -> None:
        def visit(value: Any, current_file: str) -> None:
            if isinstance(value, dict):
                if "$ref" in value:
                    self.schema_ref(value["$ref"], current_file)
                for child in value.values():
                    visit(child, current_file)
            elif isinstance(value, list):
                for child in value:
                    visit(child, current_file)

        for filename, schema in self.schemas.items():
            visit(schema, filename)


PAYLOAD_SCHEMAS: dict[str, tuple[str, str | None]] = {
    "hello": ("auth.schema.json", "hello"),
    "pairing_request": ("pairing.schema.json", "request"),
    "pairing_accept": ("pairing.schema.json", "accept"),
    "pairing_reject": ("pairing.schema.json", "reject"),
    "auth_challenge": ("auth.schema.json", "challenge"),
    "auth_response": ("auth.schema.json", "response"),
    "auth_success": ("auth.schema.json", "success"),
    "notification_event": ("notification.schema.json", None),
    "event_transfer_start": ("event-transfer.schema.json", "start"),
    "event_transfer_chunk": ("event-transfer.schema.json", "chunk"),
    "event_transfer_complete": ("event-transfer.schema.json", "complete"),
    "event_ack": ("ack.schema.json", "event"),
    "asset_missing_request": ("asset.schema.json", "missingRequest"),
    "asset_transfer_start": ("asset.schema.json", "start"),
    "asset_transfer_chunk": ("asset.schema.json", "chunk"),
    "asset_transfer_complete": ("asset.schema.json", "complete"),
    "asset_ack": ("ack.schema.json", "asset"),
    "ping": ("control.schema.json", "keepalive"),
    "pong": ("control.schema.json", "keepalive"),
    "error": ("control.schema.json", "error"),
}

PRE_SESSION_TYPES = {
    "hello", "pairing_request", "pairing_accept", "pairing_reject",
    "auth_challenge", "auth_response", "auth_success",
}


def payload_schema(validator: SchemaValidator, message_type: str) -> tuple[dict[str, Any], str]:
    filename, definition = PAYLOAD_SCHEMAS[message_type]
    schema = validator.schemas[filename]
    if definition is not None:
        schema = schema["$defs"][definition]
    return schema, filename


def validate_semantics(document: dict[str, Any], filename: str) -> None:
    message_type = document["message_type"]
    payload = document["payload"]
    if message_type not in PRE_SESSION_TYPES:
        if document["session_id"] is None or document["authentication"] is None:
            raise ValidationError(f"{filename}: post-pairing message requires session_id and authentication")

    authentication = document["authentication"]
    if authentication is not None:
        actual_payload_hash = sha256_id(canonical_json(payload))
        if not hmac.compare_digest(actual_payload_hash, authentication["payload_sha256"]):
            raise ValidationError(f"{filename}: authenticated payload hash mismatch")
        signing_input = "\n".join([
            str(document["protocol_version"]), document["message_id"], message_type,
            document["device_id"], document["sent_at_utc"], document["session_id"],
            actual_payload_hash,
        ]).encode("utf-8")
        actual_signature = hmac.new(FIXTURE_SECRET, signing_input, hashlib.sha256).hexdigest()
        if not hmac.compare_digest(actual_signature, authentication["signature"]):
            raise ValidationError(f"{filename}: fixture HMAC mismatch")

    if message_type == "notification_event":
        canonical_raw = canonical_json(payload["raw_snapshot"])
        if len(canonical_raw) != payload["raw_snapshot_byte_size"]:
            raise ValidationError(f"{filename}: raw_snapshot_byte_size mismatch")
        if sha256_id(canonical_raw) != payload["raw_snapshot_sha256"]:
            raise ValidationError(f"{filename}: raw_snapshot_sha256 mismatch")

    if message_type == "asset_transfer_start":
        inline = payload["inline_data_b64"]
        count = payload["chunk_count"]
        chunk_size = payload["chunk_size"]
        if count == 0:
            if inline is None or chunk_size != 0:
                raise ValidationError(f"{filename}: inline transfer requires data and zero chunk_size")
            decoded = base64.b64decode(inline, validate=True)
            if len(decoded) != payload["encoded_size"]:
                raise ValidationError(f"{filename}: inline encoded_size mismatch")
            if payload["content_encoding"] == "none":
                if len(decoded) != payload["canonical_size"] or sha256_id(decoded) != payload["asset_hash"]:
                    raise ValidationError(f"{filename}: inline canonical bytes/hash mismatch")
            if payload["encoded_size"] >= 65536:
                raise ValidationError(f"{filename}: assets >=64 KiB must be chunked")
        elif inline is not None or chunk_size < 1:
            raise ValidationError(f"{filename}: chunked transfer cannot include inline data")


def validate_document(validator: SchemaValidator, document: Any, filename: str) -> None:
    if filename.startswith("pairing-qr"):
        validator.validate(document, validator.schemas["pairing.schema.json"]["$defs"]["qr"], "pairing.schema.json")
        return
    validator.validate(document, validator.schemas["envelope.schema.json"], "envelope.schema.json")
    message_type = document["message_type"]
    if message_type not in PAYLOAD_SCHEMAS:
        raise ValidationError(f"{filename}: no payload schema for {message_type!r}")
    schema, schema_file = payload_schema(validator, message_type)
    validator.validate(document["payload"], schema, schema_file, "$.payload")
    validate_semantics(document, filename)


def verify_vector() -> None:
    vector = json.loads((ROOT / "vectors" / "hmac-sha256.json").read_text(encoding="utf-8"))
    envelope = vector["envelope"]
    canonical = canonical_json(envelope["payload"])
    if canonical.decode("utf-8") != vector["canonical_payload_utf8"]:
        raise ValidationError("canonical JSON vector mismatch")
    payload_hash = sha256_id(canonical)
    if payload_hash != vector["payload_sha256"]:
        raise ValidationError("payload hash vector mismatch")
    signing_input = "\n".join([
        str(envelope["protocol_version"]), envelope["message_id"], envelope["message_type"],
        envelope["device_id"], envelope["sent_at_utc"], envelope["session_id"], payload_hash,
    ])
    if signing_input != vector["signing_input_utf8"]:
        raise ValidationError("signing input vector mismatch")
    secret = b64url_decode_no_padding(vector["secret_b64url"])
    signature = hmac.new(secret, signing_input.encode("utf-8"), hashlib.sha256).hexdigest()
    if not hmac.compare_digest(signature, vector["signature_hex"]):
        raise ValidationError("HMAC vector mismatch")


def main() -> int:
    validator = SchemaValidator(SCHEMA_DIR)
    validator.verify_references()

    envelope_types = set(validator.schemas["envelope.schema.json"]["properties"]["message_type"]["enum"])
    if envelope_types != set(PAYLOAD_SCHEMAS):
        raise ValidationError(f"message type/schema map mismatch: {envelope_types ^ set(PAYLOAD_SCHEMAS)}")

    valid_count = 0
    for path in sorted(VALID_DIR.glob("*.json")):
        validate_document(validator, json.loads(path.read_text(encoding="utf-8")), path.name)
        valid_count += 1

    invalid_count = 0
    false_accepts: list[str] = []
    for path in sorted(INVALID_DIR.glob("*.json")):
        try:
            validate_document(validator, json.loads(path.read_text(encoding="utf-8")), path.name)
        except (ValidationError, ValueError, KeyError, TypeError):
            invalid_count += 1
        else:
            false_accepts.append(path.name)
    if false_accepts:
        raise ValidationError(f"invalid fixtures unexpectedly accepted: {false_accepts}")

    verify_vector()
    print(f"PASS: {len(validator.schemas)} schemas, {valid_count} valid fixtures, "
          f"{invalid_count} invalid fixtures, 1 HMAC vector")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValidationError, json.JSONDecodeError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
