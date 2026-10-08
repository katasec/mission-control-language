"""Calculate capped retry delays; scheduling and execution belong to the caller."""

from decimal import Decimal
from fractions import Fraction
from numbers import Integral, Real


def retry_delay(attempt, base_seconds, max_seconds):
    """Return min(base_seconds * 2**(attempt - 1), max_seconds).

    Inputs must be a positive integer attempt and positive finite real
    durations; booleans are invalid. Invalid inputs raise ValueError.
    Uncapped doubled results are exact Fractions. The first uncapped attempt
    returns the original base, and capped results return the original cap.
    No sleeping, execution, retry policy, or persistent state is involved.
    """
    if isinstance(attempt, bool) or not isinstance(attempt, Integral) or attempt < 1:
        raise ValueError("attempt must be an integer starting at 1")

    durations = []
    for name, value in (("base_seconds", base_seconds), ("max_seconds", max_seconds)):
        if isinstance(value, bool) or not isinstance(value, (Real, Decimal)):
            raise ValueError(f"{name} must be a positive finite real number")
        try:
            exact = Fraction(value)
        except (TypeError, ValueError, OverflowError) as exc:
            raise ValueError(f"{name} must be a positive finite real number") from exc
        if exact <= 0:
            raise ValueError(f"{name} must be a positive finite real number")
        durations.append(exact)

    delay, cap = durations
    if delay >= cap:
        return max_seconds
    if attempt == 1:
        return base_seconds

    remaining = attempt - 1
    while remaining > 0:
        # Check the cap before doubling, so intermediates remain below it.
        if delay >= cap - delay:
            return max_seconds
        delay *= 2
        remaining -= 1
    return delay
