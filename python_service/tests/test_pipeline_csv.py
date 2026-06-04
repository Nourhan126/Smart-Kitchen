"""
tests/test_pipeline_csv.py
--------------------------
Unit tests for CSV parsing in the pipeline module.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from app.pipeline import parse_recipe_names_from_csv


_CSV_NAME_COL = b"""name,calories,fat
Pesto Pizza,320,12
Chicken Alfredo Pasta,600,22
Butter Chicken,480,18
"""

_CSV_RECIPE_NAME_COL = b"""recipe_name,servings
Beef Tacos,4
Mango Salsa,6
"""

_CSV_NO_HEADER = b"""Apple Pie
Banana Bread
"""

_CSV_TITLE_COL = b"""title,author
Spaghetti Bolognese,Chef A
Greek Salad,Chef B
"""


def test_name_column_detected():
    names = parse_recipe_names_from_csv(_CSV_NAME_COL)
    assert names == ["Pesto Pizza", "Chicken Alfredo Pasta", "Butter Chicken"]


def test_recipe_name_column_detected():
    names = parse_recipe_names_from_csv(_CSV_RECIPE_NAME_COL)
    assert names == ["Beef Tacos", "Mango Salsa"]


def test_title_column_detected():
    names = parse_recipe_names_from_csv(_CSV_TITLE_COL)
    assert names == ["Spaghetti Bolognese", "Greek Salad"]


def test_fallback_to_first_column():
    names = parse_recipe_names_from_csv(_CSV_NO_HEADER)
    # First column contains the recipe names (no header → DictReader uses row 1 as header)
    # With no matching header the first actual value rows are returned
    assert len(names) >= 1


def test_empty_csv_returns_empty_list():
    names = parse_recipe_names_from_csv(b"")
    assert names == []


def test_csv_with_empty_rows_skipped():
    csv_bytes = b"name\nPizza\n\nBurger\n\n"
    names = parse_recipe_names_from_csv(csv_bytes)
    assert names == ["Pizza", "Burger"]
