"""Calculate retry delays; retry orchestration belongs to the caller."""

import math


def _validate_seconds(value, name):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{name} must be a positive finite int or float")
    if value <= 0 or (isinstance(value, float) and not math.isfinite(value)):
        raise ValueError(f"{name} must be a positive finite int or float")


def retry_delay(attempt, base_seconds, max_seconds):
    """Return a capped exponential delay, without sleeping or running jobs.

    Attempts start at one. Seconds must be positive finite Python ints or
    floats; booleans are not accepted in any position. Invalid input raises
    ValueError. Uncapped float delays beyond the float range return exact ints.
    The number of doublings is bounded by the numeric bounds, not by attempt.
    """
    if isinstance(attempt, bool) or not isinstance(attempt, int) or attempt < 1:
        raise ValueError("attempt must be an integer starting at 1")
    _validate_seconds(base_seconds, "base_seconds")
    _validate_seconds(max_seconds, "max_seconds")

    if base_seconds >= max_seconds:
        return max_seconds
    if attempt == 1:
        return base_seconds

    p, q = (base_seconds, 1) if isinstance(base_seconds, int) else base_seconds.as_integer_ratio()
    r, s = (max_seconds, 1) if isinstance(max_seconds, int) else max_seconds.as_integer_ratio()
    completed = 0
    remaining = attempt - 1
    cap_product = r * q
    while completed < remaining:
        next_p = p * 2
        if next_p * s >= cap_product:
            return max_seconds
        p = next_p
        completed += 1

    if isinstance(base_seconds, int):
        return p
    try:
        return math.ldexp(base_seconds, completed)
    except OverflowError:
        # A power-of-two-scaled float beyond the float range is integral.
        return p // q
