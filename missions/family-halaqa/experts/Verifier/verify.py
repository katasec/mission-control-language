#!/usr/bin/env python3
"""Verifier (role: judge): prove the produced PDF is a *faithful* transformation of the source —
the load-bearing trust step for religious material. It confirms, by content-hash and not by
trusting the editor, that:
  1. every page the human asked to KEEP is byte-identical to the source (nothing rewritten),
  2. every page the human asked to REMOVE is absent,
  3. a cover page was prepended,
  4. the final page count is exactly (source - removed + 1).

stdin : {"output": "<Editor result JSON>"}
stdout: {"verdict": "pass"|"fail", "status": "pass"|"fail", "reason": <str|null>}
"""
import sys, json, hashlib


def emit(status, reason=None):
    print(json.dumps({"verdict": status, "status": status, "reason": reason}))
    sys.exit(0)


def page_content_hash(page):
    """SHA-256 over the raw bytes of a page's content stream(s) — an alteration fingerprint."""
    import pikepdf
    h = hashlib.sha256()
    contents = page.obj.get("/Contents")
    if contents is None:
        return h.hexdigest()
    streams = contents if isinstance(contents, pikepdf.Array) else [contents]
    for s in streams:
        try:
            h.update(bytes(s.read_raw_bytes()))
        except Exception:
            h.update(bytes(s.read_bytes()))
    return h.hexdigest()


def main():
    import pikepdf
    data = json.load(sys.stdin)
    result = data.get("output")
    if isinstance(result, str):
        result = json.loads(result)
    if not result:
        emit("fail", "Verifier received no editor result to check.")

    source_pdf = result["source_pdf"]
    output_pdf = result["output_pdf"]
    removed    = sorted({int(p) for p in result.get("removed", [])})

    src  = pikepdf.open(source_pdf)
    prod = pikepdf.open(output_pdf)
    n    = len(src.pages)
    keep = [i for i in range(1, n + 1) if i not in removed]
    expected = n - len(removed) + 1

    # (4) page count
    if len(prod.pages) != expected:
        emit("fail", f"Expected {expected} pages (source {n} - removed {len(removed)} + 1 cover), "
                     f"got {len(prod.pages)}.")

    # (3) a cover was prepended — produced[0] must NOT be any source page's content
    src_hashes = {page_content_hash(src.pages[i - 1]) for i in range(1, n + 1)}
    if page_content_hash(prod.pages[0]) in src_hashes:
        emit("fail", "First page is not a new cover — it matches an existing source page.")

    # (1) kept-page fidelity: produced[k+1] byte-identical to source[keep[k]]
    for k, src_pg in enumerate(keep):
        if page_content_hash(src.pages[src_pg - 1]) != page_content_hash(prod.pages[k + 1]):
            emit("fail", f"Kept source page {src_pg} was altered in the output (position {k + 2}).")

    # (2) removed pages absent: their content hashes must not appear among produced body pages
    prod_body = {page_content_hash(prod.pages[j]) for j in range(1, len(prod.pages))}
    for r in removed:
        if page_content_hash(src.pages[r - 1]) in prod_body:
            emit("fail", f"Page {r} was supposed to be removed but is still present.")

    emit("pass", None)


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        emit("fail", f"Verifier error: {type(e).__name__}: {e}")
