namespace GameEngine.Game;

public enum Rank
{
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14
}

public class Card
{
    public Rank Rank { get; }
    public int Suit { get; } // 0-3

    public Card(Rank rank, int suit)
    {
        Rank = rank;
        Suit = suit;
    }

    public override string ToString()
    {
        string rankStr = Rank switch
        {
            Rank.Six => "6",
            Rank.Seven => "7",
            Rank.Eight => "8",
            Rank.Nine => "9",
            Rank.Ten => "10",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => Rank.ToString()
        };
        return $"{rankStr}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is Card other)
        {
            return Rank == other.Rank && Suit == other.Suit;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Rank, Suit);
    }
}