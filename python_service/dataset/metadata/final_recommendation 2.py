# -*- coding: utf-8 -*-

"""
FINAL HYBRID RECIPE RECOMMENDATION SYSTEM
FASTAPI READY VERSION
SMART KITCHEN AI
"""

import pandas as pd
import numpy as np
import re
import json
from difflib import get_close_matches

print("=" * 70)
print(" HYBRID RECIPE RECOMMENDER (FINAL IMPROVED VERSION)")
print("=" * 70)

# ================= LOAD DATA =================

try:

    df = pd.read_csv(
        'dataset/metadata/RAW_recipes after cleaning.csv'
    )

    df = df[
        [
            'name',
            'ingredients',
            'minutes',
            'n_steps',
            'n_ingredients',
            'description'
        ]
    ].dropna().reset_index(drop=True)

    def clean_text(text):

        text = str(text).lower()

        text = re.sub(
            r'[^a-zA-Z ]',
            ' ',
            text
        )

        return " ".join(text.split())

    df['ingredients_clean'] = df['ingredients'].apply(clean_text)

    print(f"✓ Loaded {len(df)} recipes")

except Exception as e:

    print(f"Dataset loading error: {e}")

    df = pd.DataFrame()

# ================= SUBSTITUTES =================

substitutes = {

    "chicken": [
        "turkey breast",
        "duck meat",
        "tofu",
        "lean beef strips"
    ],

    "beef": [
        "lamb",
        "chicken thigh",
        "ground turkey",
        "mushrooms"
    ],

    "milk": [
        "almond milk",
        "oat milk",
        "soy milk",
        "coconut milk"
    ],

    "egg": [
        "banana (for baking)",
        "flaxseed gel",
        "chia seed gel",
        "yogurt"
    ],

    "butter": [
        "olive oil",
        "ghee",
        "coconut oil"
    ],

    "rice": [
        "quinoa",
        "couscous",
        "bulgur wheat",
        "cauliflower rice"
    ],

    "pasta": [
        "rice noodles",
        "zucchini noodles",
        "spaghetti squash"
    ],

    "tomato": [
        "red bell pepper puree",
        "tomato paste",
        "sun-dried tomatoes"
    ],

    "onion": [
        "shallots",
        "leeks",
        "green onions"
    ],

    "garlic": [
        "garlic powder",
        "roasted garlic"
    ],

    "cheese": [
        "paneer",
        "ricotta",
        "mozzarella",
        "tofu"
    ],

    "cream": [
        "coconut cream",
        "Greek yogurt",
        "evaporated milk"
    ],

    "oil": [
        "olive oil",
        "sunflower oil",
        "canola oil"
    ],

    "bread": [
        "tortilla wraps",
        "pita bread",
        "crackers"
    ],

    "sugar": [
        "honey",
        "maple syrup",
        "date syrup"
    ],

    "flour": [
        "oat flour",
        "almond flour",
        "coconut flour"
    ],

    "salt": [
        "soy sauce",
        "sea salt",
        "seasoned salt"
    ],

    "pepper": [
        "white pepper",
        "chili flakes",
        "paprika"
    ]
}

# ================= REMOVE WORDS =================

remove_words = [

    "fresh",
    "chopped",
    "ground",
    "large",
    "small",
    "diced",
    "sliced",
    "minced",
    "crushed",
    "cups",
    "cup",
    "tbsp",
    "tsp",
    "tablespoon",
    "teaspoon"
]

# ================= NORMALIZE =================

def normalize_ingredient(ingredient):

    ingredient = ingredient.lower()

    for word in remove_words:
        ingredient = ingredient.replace(word, "")

    ingredient = re.sub(
        r'[^a-zA-Z ]',
        '',
        ingredient
    )

    ingredient = ingredient.strip()

    if ingredient.endswith('s'):
        ingredient = ingredient[:-1]

    return ingredient

# ================= SUBSTITUTE ENGINE =================

def get_substitutes(ingredient):

    ing = normalize_ingredient(ingredient)

    if ing in substitutes:
        return substitutes[ing]

    close = get_close_matches(
        ing,
        substitutes.keys(),
        n=1,
        cutoff=0.7
    )

    if close:
        return substitutes[close[0]]

    return [
        "This ingredient is simple, so a substitute is not necessary."
    ]

# ================= DIFFICULTY =================

def compute_difficulty(
    minutes,
    n_steps,
    n_ingredients
):

    if minutes > 90:
        return 'Hard'

    elif minutes > 45:
        return 'Medium'

    else:

        if n_steps <= 8 and n_ingredients <= 8:
            return 'Easy'

        elif n_steps <= 15 and n_ingredients <= 12:
            return 'Medium'

        else:
            return 'Hard'

# ================= MAIN HYBRID ENGINE =================

def hybrid_recommend(
    user_ingredients,
    top_n=5
):

    user_ingredients = [
        normalize_ingredient(i)
        for i in user_ingredients
    ]

    user_set = set(user_ingredients)

    results = []

    if df.empty:
        return []

    for _, row in df.iterrows():

        recipe_set = set(
            row['ingredients_clean'].split()
        )

        common = user_set & recipe_set

        # ================= SCORING =================

        user_coverage = len(common) / (
            len(user_set) + 1e-9
        )

        recipe_coverage = len(common) / (
            len(recipe_set) + 1e-9
        )

        score = (
            0.5 * user_coverage
            +
            0.5 * recipe_coverage
        )

        score = min(score, 0.98)

        # ================= MISSING =================

        missing = list(recipe_set - user_set)

        missing_subs = {}

        for m in missing[:8]:
            missing_subs[m] = get_substitutes(m)

        # ================= RESULT =================

        minutes = int(row['minutes'])
        n_steps = int(row['n_steps'])
        n_ingredients = int(row['n_ingredients'])

        description = str(
            row.get('description', '')
        )

        if description == "nan":
            description = ""

        results.append({

            "recipe_name": row['name'],

            "score": round(score, 3),

            "match_percentage": round(
                user_coverage * 100,
                1
            ),

            "recipe_coverage": round(
                recipe_coverage * 100,
                1
            ),

            "prep_time_minutes": minutes,

            "difficulty": compute_difficulty(
                minutes,
                n_steps,
                n_ingredients
            ),

            "ingredients_count": n_ingredients,

            "steps_count": n_steps,

            "matched_ingredients": list(common),

            "missing_ingredients": missing[:8],

            "substitutes": missing_subs,

            "short_description": (
                description[:120] + "..."
                if len(description) > 120
                else description
            )
        })

    results = sorted(
        results,
        key=lambda x: x["score"],
        reverse=True
    )

    return results[:top_n]

# ================= FASTAPI MAIN FUNCTION =================

def get_seasonal_recommendations(
    ingredients,
    count=5,
    season=None
):

    recommendations = hybrid_recommend(
        user_ingredients=ingredients,
        top_n=count
    )

    return {

        "recommendations": recommendations,

        "missing_ingredients": [],

        "substitutes": {},

        "meta": {

            "season": season,

            "count": len(recommendations),

            "input_ingredients": ingredients,

            "system": "Smart Kitchen AI",

            "model": "Hybrid Recommendation Engine"
        }
    }

# ================= OPTIONAL LOCAL TEST =================

if __name__ == "__main__":

    print("\n INTERACTIVE MODE (type exit to stop)")

    while True:

        user_input = input(
            "\nEnter ingredients (comma separated): "
        )

        if user_input.lower() == "exit":

            print("Goodbye!")

            break

        ingredients = [

            i.strip().lower()

            for i in user_input.split(",")
        ]

        print("\n Searching...\n")

        recs = get_seasonal_recommendations(

            ingredients=ingredients,

            count=5,

            season="winter"
        )

        print(
            json.dumps(
                recs,
                indent=4,
                ensure_ascii=False
            )
        )