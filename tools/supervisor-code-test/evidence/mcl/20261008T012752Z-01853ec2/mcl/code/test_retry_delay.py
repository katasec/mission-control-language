"""Standard-library tests for the calculation-only retry delay helper."""

import math
import sys
import unittest

from retry_delay import retry_delay


class RetryDelayTests(unittest.TestCase):
    def test_first_attempt_and_doubling(self):
        for attempt, expected in ((1, 3), (2, 6), (3, 12), (4, 24)):
            with self.subTest(attempt=attempt):
                self.assertEqual(retry_delay(attempt, 3, 100), expected)
        base = 0.125
        self.assertIs(retry_delay(1, base, 10), base)
        self.assertEqual(retry_delay(4, base, 10), 1.0)

    def test_cap_exact_crossed_and_initial(self):
        cap = 12.0
        self.assertIs(retry_delay(3, 3, cap), cap)
        self.assertIs(retry_delay(4, 3, cap), cap)
        self.assertIs(retry_delay(1, 12, cap), cap)
        self.assertIs(retry_delay(1, 20, cap), cap)
        self.assertEqual(retry_delay(3, 3, 10), 10)

    def test_huge_attempt(self):
        huge = 10 ** 1000
        self.assertEqual(retry_delay(huge, 1, 60), 60)
        self.assertEqual(retry_delay(huge, 1e308, 10 ** 400), 10 ** 400)
        self.assertEqual(retry_delay(huge, float.fromhex('0x0.0000000000001p-1022'), sys.float_info.max), sys.float_info.max)

    def test_fractional_and_subnormal_floats(self):
        self.assertEqual(retry_delay(3, 0.1, 10), math.ldexp(0.1, 2))
        smallest = float.fromhex('0x0.0000000000001p-1022')
        self.assertEqual(retry_delay(2, smallest, 1), smallest * 2)
        self.assertEqual(retry_delay(1075, smallest, 2), 1.0)
        self.assertEqual(retry_delay(1076, smallest, 2), 2)

    def test_large_integers(self):
        base = 10 ** 400
        self.assertEqual(retry_delay(2, base, base * 10), base * 2)
        self.assertEqual(retry_delay(5, base, base * 10), base * 10)
        self.assertEqual(retry_delay(2, 1, 10 ** 400), 2)

    def test_float_range_and_integer_overflow_fallback(self):
        largest = sys.float_info.max
        self.assertEqual(retry_delay(2, largest / 2, largest), largest)
        self.assertEqual(retry_delay(2, largest, largest), largest)
        base = 1e308
        numerator, denominator = base.as_integer_ratio()
        for attempt in (2, 3, 10):
            with self.subTest(attempt=attempt):
                result = retry_delay(attempt, base, 10 ** 400)
                self.assertIsInstance(result, int)
                self.assertEqual(result, numerator * 2 ** (attempt - 1) // denominator)
        self.assertEqual(retry_delay(2, largest, 10 ** 400), int(largest) * 2)

    def test_invalid_attempts(self):
        for value in (0, -1, True, False, 1.0, '1', None, [], complex(1), float('nan'), float('inf')):
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    retry_delay(value, 1, 10)

    def test_invalid_seconds_in_each_position(self):
        invalid = (True, False, 0, -1, 0.0, -0.5, '1', None, [], {}, complex(1), float('nan'), float('inf'), float('-inf'))
        for value in invalid:
            for position in (1, 2):
                with self.subTest(value=value, position=position):
                    args = [1, 1, 10]
                    args[position] = value
                    with self.assertRaises(ValueError):
                        retry_delay(*args)

    def test_validation_before_early_return(self):
        for args in ((1, 10, True), (1, 10, float('nan')), (1, 10, float('inf')), (1, float('inf'), 1), (1, 2, 0), (True, 10, 1)):
            with self.subTest(args=args):
                with self.assertRaises(ValueError):
                    retry_delay(*args)

    def test_repeatability(self):
        results = [retry_delay(5, 0.3, 100) for _ in range(5)]
        self.assertEqual(results, [math.ldexp(0.3, 4)] * 5)


if __name__ == '__main__':
    unittest.main()
