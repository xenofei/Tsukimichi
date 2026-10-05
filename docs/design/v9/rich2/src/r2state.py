"""One progress state for every mock (round 2, game designer m3, UX M2): the player is on The Moon Road, stage 3, with
Cid Garlond (Brass Wings). Stages 1 and 2 are won; stages 4 to 11 are not reached. In their story the player has met
everyone of stages 1 and 3 but not Alisaie, so the twins (stage 2, won) show face down (the spoiler shield).

Stage 3, "The Night Skyway": Cid's airship carries the road over Eorzea by night, so its five levels are seen from the
air: the moogle post over the Shroud, Ishgard above the clouds, the airship road on the chart.
"""
import r2cast

CAMPAIGN = "The Moon Road"
STAGE = 3                      # the stage the player is on
STAGES = 11                    # on The Moon Road (stage 11 is the player's pick; Storm Post joins on The Far Shore)
UNMET = {"Multiball"}          # companions the player's story has not introduced (the shield's NPC rule)
CARRIER = r2cast.BY_ID["Wings"]
PEG_MARKS = False             # the colour-blind assist (Options); off in the mocks' state

LEVELS = [  # (code, name, state, best, board)
    ("3-1", "The Moonlit Post", "won", "214,300", "base-p3"),
    ("3-2", "The Holy See", "aced", "251,880", "base-p2"),
    ("3-3", "The Airship Road", "open", "", "base-p1"),
    ("3-4", "Above the Clouds", "locked", "", None),
    ("3-5", "The Ironworks Dock", "locked", "", None),
]
CURRENT = LEVELS[2]
ACE = {"3-1": "200,000", "3-2": "240,000", "3-3": "240,000"}


def stage_state(n):
    return "done" if n < STAGE else "here" if n == STAGE else "locked"


def char_state(c):
    """face-down (not met in the story), met (met and reached in Moonfall), or locked (met, not yet reached)."""
    if c["id"] in UNMET:
        return "back"
    if c["id"] == "Bolt":                         # Storm Post joins on The Far Shore
        return "locked"
    return "met" if c["stage"] <= STAGE else "locked"


def stage_name(n):
    if n == 11:
        return "Your Pick"
    return next(c["home"] for c in r2cast.CAST if c["stage"] == n and c["id"] != "Bolt")
