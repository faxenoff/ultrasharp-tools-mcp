#!/usr/bin/env python3
"""
Automatic conversion of slow tokenizer to fast tokenizer for TEI compatibility.
This script downloads a model, converts its tokenizer to fast version, and saves it.
"""

import sys
import os
import argparse
from pathlib import Path

def convert_tokenizer(model_id: str, output_dir: str = None):
    """Convert slow tokenizer to fast tokenizer."""
    try:
        from transformers import AutoTokenizer
        print(f"[1/3] Loading tokenizer from: {model_id}")

        # Try to load tokenizer
        try:
            tokenizer = AutoTokenizer.from_pretrained(model_id, use_fast=True)
            print(f"  ✓ Fast tokenizer already available!")

            # Check if tokenizer.json exists
            if hasattr(tokenizer, 'backend_tokenizer'):
                print(f"  ✓ Tokenizer is already fast (has backend_tokenizer)")
            else:
                print(f"  ⚠ Tokenizer loaded but might not be fast")

        except Exception as e:
            print(f"  ⚠ Could not load fast tokenizer: {e}")
            print(f"  → Attempting to load slow tokenizer and convert...")

            # Load slow tokenizer
            tokenizer = AutoTokenizer.from_pretrained(model_id, use_fast=False)
            print(f"  ✓ Slow tokenizer loaded")

            # Try to convert to fast
            print(f"[2/3] Converting to fast tokenizer...")
            try:
                # This will create fast tokenizer from slow
                fast_tokenizer = AutoTokenizer.from_pretrained(
                    model_id,
                    use_fast=True,
                    trust_remote_code=True
                )
                tokenizer = fast_tokenizer
                print(f"  ✓ Conversion successful!")
            except Exception as conv_error:
                print(f"  ✗ Conversion failed: {conv_error}")
                print(f"  → This model may not support fast tokenization")
                return False

        # Save tokenizer
        if output_dir is None:
            output_dir = f"./{model_id.replace('/', '_')}_fast"

        print(f"[3/3] Saving fast tokenizer to: {output_dir}")
        Path(output_dir).mkdir(parents=True, exist_ok=True)

        tokenizer.save_pretrained(output_dir)
        print(f"  ✓ Tokenizer saved!")

        # Verify tokenizer.json exists
        tokenizer_json = Path(output_dir) / "tokenizer.json"
        if tokenizer_json.exists():
            print(f"  ✓ tokenizer.json created successfully")
            print(f"\n=== Success! ===")
            print(f"Fast tokenizer saved to: {output_dir}")
            print(f"\nTo use with TEI, you can either:")
            print(f"  1. Upload to HuggingFace Hub")
            print(f"  2. Mount as volume: -v {os.path.abspath(output_dir)}:/model")
            print(f"     and use: --model-id /model")
            return True
        else:
            print(f"  ⚠ tokenizer.json not found after saving")
            print(f"  → This model may not support fast tokenization")
            return False

    except ImportError:
        print("✗ Error: transformers library not installed")
        print("Install with: pip install transformers")
        return False
    except Exception as e:
        print(f"✗ Error: {e}")
        import traceback
        traceback.print_exc()
        return False

def main():
    parser = argparse.ArgumentParser(
        description="Convert slow tokenizer to fast tokenizer for TEI"
    )
    parser.add_argument(
        "model_id",
        help="HuggingFace model ID (e.g., 'ibm-granite/granite-embedding-125m-english')"
    )
    parser.add_argument(
        "-o", "--output",
        help="Output directory (default: ./<model_name>_fast)",
        default=None
    )

    args = parser.parse_args()

    print("=" * 60)
    print("Fast Tokenizer Converter for TEI")
    print("=" * 60)
    print()

    success = convert_tokenizer(args.model_id, args.output)
    sys.exit(0 if success else 1)

if __name__ == "__main__":
    main()
