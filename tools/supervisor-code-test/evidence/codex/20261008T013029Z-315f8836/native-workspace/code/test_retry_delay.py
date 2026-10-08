"""Standard-library tests for retry delay validation and calculation."""

import unittest
from decimal import Decimal
from fractions import Fraction

from retry_delay import retry_delay


class RetryDelayTests(unittest.TestCase):
    def test_first_attempt_and_doubling(self):
        for attempt, expected in ((1, 3), (2, 6), (3, 12)):
            with self.subTest(attempt=attempt):
                self.assertEqual(retry_delay(attempt, 3, 100), expected)

    def test_exact_cap_and_overshoot(self):
        for args, expected in (((3, 3, 12), 12), ((4, 3, 10), 10), ((5, 3, 10), 10)):
            with self.subTest(args=args):
                self.assertEqual(retry_delay(*args), expected)

    def test_base_at_or_above_cap(self):
        for attempt in (1, 2, 10**100):
            for base in (10, 20):
                with self.subTest(attempt=attempt, base=base):
                    self.assertEqual(retry_delay(attempt, base, 10), 10)

    def test_huge_attempt(self):
        self.assertEqual(retry_delay(10**100, 0.5, 60), 60)

    def test_float_extremes(self):
        smallest = float.fromhex("0x0.0000000000001p-1022")
        largest = float.fromhex("0x1.fffffffffffffp+1023")
        self.assertEqual(retry_delay(2, smallest, largest), smallest * 2)
        self.assertEqual(retry_delay(10**100, smallest, largest), largest)
        self.assertEqual(retry_delay(2, largest, largest), largest)

    def test_large_integer_durations(self):
        base = 10**400
        self.assertEqual(retry_delay(2, base, base * 10), base * 2)
        self.assertEqual(retry_delay(10**100, base, base * 10), base * 10)

    def test_fractional_and_mixed_durations(self):
        cases = (
            ((3, 0.125, 10), Fraction(1, 2)),
            ((2, Fraction(1, 3), Decimal("0.7")), Fraction(2, 3)),
            ((3, Decimal("0.1"), Fraction(1, 2)), Fraction(2, 5)),
            ((2, 0.5, 10**400), 1),
            ((2, 10**400, 0.5), 0.5),
        )
        for args, expected in cases:
            with self.subTest(args=args):
                self.assertEqual(retry_delay(*args), expected)

    def test_documented_return_values(self):
        base = Decimal("0.1")
        cap = Decimal("0.5")
        self.assertIs(retry_delay(1, base, cap), base)
        self.assertIsInstance(retry_delay(2, base, cap), Fraction)
        self.assertIs(retry_delay(4, base, cap), cap)

    def test_invalid_attempts(self):
        for attempt in (True, False, 0, -1, 1.0, "1", None, 1j, Fraction(1, 1)):
            with self.subTest(attempt=attempt):
                with self.assertRaises(ValueError):
                    retry_delay(attempt, 1, 10)

    def test_invalid_duration_types_and_signs(self):
        invalid = (True, False, "1", None, 1j, [], object(), 0, -1, 0.0,
                   Fraction(-1, 2), Decimal("0"), Decimal("-1"))
        for value in invalid:
            for position in (1, 2):
                with self.subTest(value=value, position=position):
                    args = [1, 1, 10]
                    args[position] = value
                    with self.assertRaises(ValueError):
                        retry_delay(*args)

    def test_nonfinite_durations(self):
        invalid = (float("nan"), float("inf"), float("-inf"),
                   Decimal("NaN"), Decimal("sNaN"),
                   Decimal("Infinity"), Decimal("-Infinity"))
        for value in invalid:
            for position in (1, 2):
                with self.subTest(value=value, position=position):
                    args = [1, 1, 10]
                    args[position] = value
                    with self.assertRaises(ValueError):
                        retry_delay(*args)

    def test_validation_before_early_cap_return(self):
        for args in ((0, 20, 10), (True, 20, 10), (1, 20, False), (1, float("inf"), 10)):
            with self.subTest(args=args):
                with self.assertRaises(ValueError):
                    retry_delay(*args)


if __name__ == "__main__":
    unittest.main()
