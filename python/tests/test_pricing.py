import pytest

from ai_lab.pricing import cost_usd


def test_cost_counts_input_and_output() -> None:
    # 1,000 in at $0.15 per 1M + 500 out at $0.60 per 1M = $0.00015 + $0.0003
    assert cost_usd(1_000, 500, price_in=0.15, price_out=0.60) == pytest.approx(0.00045)


def test_local_model_costs_nothing() -> None:
    assert cost_usd(50_000, 2_000, price_in=0.0, price_out=0.0) == 0.0


def test_output_tokens_priced_separately() -> None:
    only_input = cost_usd(1_000_000, 0, price_in=1.0, price_out=4.0)
    only_output = cost_usd(0, 1_000_000, price_in=1.0, price_out=4.0)
    assert (only_input, only_output) == (pytest.approx(1.0), pytest.approx(4.0))


def test_negative_tokens_rejected() -> None:
    with pytest.raises(ValueError, match="negative"):
        cost_usd(-1, 0, price_in=0.15, price_out=0.60)
