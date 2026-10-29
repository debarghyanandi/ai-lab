"""Token cost maths, the AI 7 starter module.

Providers quote prices in US dollars per 1M tokens, input and output priced separately.
"""

PER_MILLION = 1_000_000


def cost_usd(input_tokens: int, output_tokens: int, *, price_in: float, price_out: float) -> float:
    """Return the cost of one model call in US dollars.

    price_in and price_out are USD per 1M tokens. They are keyword-only (after the `*`),
    so a call can never swap them by position.
    """
    if input_tokens < 0 or output_tokens < 0:
        raise ValueError("token counts cannot be negative")
    return (input_tokens * price_in + output_tokens * price_out) / PER_MILLION
