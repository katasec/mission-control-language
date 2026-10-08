import math
import unittest
from retry_delay import retry_delay

class Acceptance(unittest.TestCase):
    def test_first(self):
        self.assertEqual(retry_delay(1, 2, 60), 2)
    def test_double(self):
        self.assertEqual(retry_delay(4, 2, 60), 16)
    def test_cap(self):
        self.assertEqual(retry_delay(20, 2, 60), 60)
    def test_base_over_cap(self):
        self.assertEqual(retry_delay(1, 100, 3), 3)
    def test_huge_attempt(self):
        self.assertEqual(retry_delay(10**1000, 0.25, 100), 100)
    def test_overflow_boundary(self):
        self.assertEqual(retry_delay(3, 1e308, 1.5e308), 1.5e308)
    def test_subnormal(self):
        self.assertEqual(retry_delay(2, 5e-324, 1.0), 1e-323)
    def test_invalid_attempt(self):
        for value in (True,False,0,-1,1.0,"1",None):
            with self.subTest(value=value),self.assertRaises(ValueError):
                retry_delay(value,1,10)
    def test_invalid_numbers(self):
        for value in (True,False,0,-1,float("inf"),float("-inf"),float("nan"),"1",None,1j):
            for position in (1,2):
                arguments=[1,1,10];arguments[position]=value
                with self.subTest(value=value,position=position),self.assertRaises(ValueError):
                    retry_delay(*arguments)
    def test_small_matrix(self):
        for base in (0.125,1,2.5,17):
            for cap in (0.25,5,100):
                for attempt in range(1,25):
                    with self.subTest(base=base,cap=cap,attempt=attempt):
                        self.assertEqual(retry_delay(attempt,base,cap),min(base*2**(attempt-1),cap))
