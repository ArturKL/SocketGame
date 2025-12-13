namespace GameEngine.Game;

public class Deck
{
    private readonly List<Card> _cards = new();
    private readonly Random _random = new();

    public Deck()
    {
        InitializeDeck();
    }

    private void InitializeDeck()
    {
        _cards.Clear();
        // Create 36 cards: 9 ranks (Six through Ace) × 4 suits
        for (int rank = (int)Rank.Six; rank <= (int)Rank.Ace; rank++)
        {
            for (int suit = 0; suit < 4; suit++)
            {
                _cards.Add(new Card((Rank)rank, suit));
            }
        }
    }

    public void Shuffle()
    {
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    public List<Card> DealCards(int count)
    {
        if (count > _cards.Count)
        {
            count = _cards.Count;
        }

        var dealt = new List<Card>(count);
        for (int i = 0; i < count; i++)
        {
            dealt.Add(_cards[^1]);
            _cards.RemoveAt(_cards.Count - 1);
        }

        return dealt;
    }

    public int RemainingCards => _cards.Count;

    public void ReturnCards(List<Card> cards)
    {
        _cards.AddRange(cards);
    }
}