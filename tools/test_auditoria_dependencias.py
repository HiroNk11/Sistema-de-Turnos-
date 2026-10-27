import unittest
from auditar_dependencias import comparar, contiene


class RangosNuget(unittest.TestCase):
    def test_limites_abiertos(self):
        self.assertTrue(contiene("9.0.11", "(, 9.0.12)"))
        self.assertFalse(contiene("9.0.12", "(, 9.0.12)"))
        self.assertFalse(contiene("9.0.21", "(, 9.0.12)"))

    def test_limites_cerrados(self):
        self.assertTrue(contiene("9.0.11", "[9.0.11, 9.0.12]"))
        self.assertTrue(contiene("9.0.12", "[9.0.11, 9.0.12]"))
        self.assertFalse(contiene("9.0.10", "[9.0.11, 9.0.12]"))

    def test_exacta(self):
        self.assertTrue(contiene("1.2.3", "[1.2.3]"))
        self.assertFalse(contiene("1.2.4", "[1.2.3]"))

    def test_sin_maximo(self):
        self.assertTrue(contiene("10.0.0", "[9.0.0, )"))

    def test_normalizacion(self):
        self.assertEqual(comparar("1.2.3", "1.2.3.0"), 0)
        self.assertEqual(comparar("1.2.3+build", "1.2.3"), 0)

    def test_pre_release(self):
        self.assertLess(comparar("1.0.0-preview.2", "1.0.0"), 0)
        self.assertLess(comparar("1.0.0-preview.2", "1.0.0-preview.10"), 0)
        self.assertLess(comparar("1.0.0-1", "1.0.0-alpha"), 0)
        self.assertEqual(comparar("1.0.0-ALPHA", "1.0.0-alpha"), 0)

    def test_falla_cerrado(self):
        with self.assertRaises(ValueError):
            contiene("1.0.0", "no es un rango")


if __name__ == "__main__":
    unittest.main()
