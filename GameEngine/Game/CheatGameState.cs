namespace GameEngine.Game;

public enum RoundPhase
{
    WaitingForClaim,
    Playing,
    CheckingBluff
}

public class RoundCard
{
    public Card Card { get; }
    public string PlayerId { get; }

    public RoundCard(Card card, string playerId)
    {
        Card = card;
        PlayerId = playerId;
    }
}

public class CheatGameState
{
    public List<string> PlayerOrder { get; private set; } = new();
    public int PlayerCount { get; private set; }

    private readonly Dictionary<string, List<Card>> _playerHands = new();

    // Current round state
    public Rank? ClaimedRank { get; private set; }
    public List<RoundCard> CardsInPlay { get; private set; } = new();
    public int CurrentPlayerIndex { get; private set; }
    public RoundPhase Phase { get; private set; } = RoundPhase.WaitingForClaim;
    public int LastBatchCount { get; private set; } = 0;

    public bool IsStarted { get; private set; }

    public string? WinnerId { get; private set; }

    public bool IsGameEnded => WinnerId != null;

    public void InitializeGame(List<string> playerIds)
    {
        PlayerCount = playerIds.Count;
        if (PlayerCount < 2)
        {
            throw new ArgumentException("Need at least 2 players");
        }

        PlayerOrder = new List<string>(playerIds);
        _playerHands.Clear();
        foreach (var id in playerIds)
        {
            _playerHands[id] = new List<Card>();
        }

        IsStarted = true;
        CurrentPlayerIndex = 0;
        Phase = RoundPhase.WaitingForClaim;
    }

    public void DealCards(Deck deck)
    {
        if (!IsStarted)
        {
            throw new InvalidOperationException("Game not started");
        }

        deck.Shuffle();

        int totalCards = 36;
        int cardsPerPlayer = totalCards / PlayerCount;
        int remainder = totalCards % PlayerCount;

        // Deal cards
        int cardIndex = 0;
        foreach (var playerId in PlayerOrder)
        {
            int cardsToDeal = cardsPerPlayer + (cardIndex < remainder ? 1 : 0);
            var cards = deck.DealCards(cardsToDeal);
            _playerHands[playerId] = new List<Card>(cards);
            cardIndex++;
        }

        SwapToPreventCompleteSets();
    }

    private void SwapToPreventCompleteSets()
    {
        foreach (var playerId in PlayerOrder)
        {
            var hand = _playerHands[playerId];

            // find ranks where this player has all 4 cards
            var completeRanks = hand
                .GroupBy(c => c.Rank)
                .Where(g => g.Count() == 4)
                .Select(g => g.Key)
                .ToList();

            foreach (var rank in completeRanks)
            {
                var cardToSwap = hand.First(c => c.Rank == rank);

                // try to find another player and a card in their hand that is safe to swap
                foreach (var otherId in PlayerOrder)
                {
                    if (otherId == playerId)
                    {
                        continue;
                    }

                    var otherHand = _playerHands[otherId];

                    // Find a card in otherHand whose rank won't become a 4-of-a-kind in the current player's hand.
                    var candidate = otherHand.FirstOrDefault(c => hand.Count(h => h.Rank == c.Rank) < 3);

                    if (candidate == null)
                    {
                        continue;
                    }

                    // swap
                    hand.Remove(cardToSwap);
                    otherHand.Remove(candidate);

                    hand.Add(candidate);
                    otherHand.Add(cardToSwap);

                    break;
                }
            }
        }
    }

    public List<Rank> AutoDiscardSets(string playerId)
    {
        var discarded = new List<Rank>();

        var hand = _playerHands[playerId];
        var rankGroups = hand.GroupBy(c => c.Rank).ToList();

        foreach (var group in rankGroups)
        {
            if (group.Count() == 4 && group.Key != Rank.Ace)
            {
                var cardsToRemove = group.ToList();
                foreach (var card in cardsToRemove)
                {
                    hand.Remove(card);
                }

                discarded.Add(group.Key);
            }
        }

        return discarded;
    }

    public string GetCurrentPlayerId()
    {
        if (PlayerOrder.Count == 0)
        {
            return string.Empty;
        }

        return PlayerOrder[CurrentPlayerIndex];
    }

    public void ClaimRank(Rank rank)
    {
        if (Phase != RoundPhase.WaitingForClaim)
        {
            throw new InvalidOperationException("Not waiting for rank claim");
        }

        if (rank == Rank.Ace)
        {
            throw new ArgumentException("Cannot claim aces");
        }

        ClaimedRank = rank;
        Phase = RoundPhase.Playing;
    }

    public void PutDownCards(string playerId, List<int> cardIndices)
    {
        if (Phase != RoundPhase.Playing)
        {
            throw new InvalidOperationException("Not in playing phase");
        }

        if (GetCurrentPlayerId() != playerId)
        {
            throw new InvalidOperationException("Not your turn");
        }

        if (cardIndices.Count < 1 || cardIndices.Count > 3)
        {
            throw new ArgumentException("Must put down 1-3 cards");
        }

        var hand = _playerHands[playerId];
        if (cardIndices.Any(idx => idx < 0 || idx >= hand.Count))
        {
            throw new ArgumentException("Invalid card index");
        }

        // Remove cards from hand and add to play
        var cardsToPut = cardIndices.OrderByDescending(i => i).Select(idx => hand[idx]).ToList();
        foreach (var idx in cardIndices.OrderByDescending(i => i))
        {
            hand.RemoveAt(idx);
        }

        for (int i = 0; i < cardsToPut.Count; i++)
        {
            CardsInPlay.Add(new RoundCard(cardsToPut[i], playerId));
        }

        LastBatchCount = cardsToPut.Count;

        // Check if player has no cards left - next player must call bluff
        if (hand.Count == 0)
        {
            Phase = RoundPhase.CheckingBluff;
        }

        NextPlayer();
    }

    public (bool isBluff, string loserId, Card revealedCard, bool immediateWin) CallBluff(string callerId,
        int cardNumber)
    {
        if (Phase != RoundPhase.Playing && Phase != RoundPhase.CheckingBluff)
        {
            throw new InvalidOperationException("Cannot call bluff now");
        }

        if (GetCurrentPlayerId() != callerId)
        {
            throw new InvalidOperationException("Not your turn");
        }

        if (cardNumber < 1 || cardNumber > LastBatchCount)
        {
            throw new ArgumentException($"Card index must be between 1 and {LastBatchCount}");
        }

        // Convert card number to absolute index
        int absoluteIndex = CardsInPlay.Count - 1 - LastBatchCount + cardNumber;

        if (absoluteIndex < 0 || absoluteIndex >= CardsInPlay.Count)
        {
            throw new ArgumentException("Invalid card index");
        }

        var cardToCheck = CardsInPlay[absoluteIndex];
        bool isBluff = cardToCheck.Card.Rank != ClaimedRank!;

        string loserId;
        bool immediateWin = false;

        if (isBluff)
        {
            loserId = cardToCheck.PlayerId;
        }
        else
        {
            loserId = callerId;

            // Check if the player who put down last cards wins immediately
            var playerWhoPutCard = cardToCheck.PlayerId;
            if (_playerHands[playerWhoPutCard].Count == 0)
            {
                WinnerId = playerWhoPutCard;
                immediateWin = true;
            }
        }

        Phase = RoundPhase.CheckingBluff;
        return (isBluff, loserId, cardToCheck.Card, immediateWin);
    }

    public List<Rank> EndRound(string loserId)
    {
        if (Phase != RoundPhase.CheckingBluff)
        {
            throw new InvalidOperationException("Round not ended");
        }

        // Give all cards in play to the loser
        var cardsToGive = CardsInPlay.Select(rc => rc.Card).ToList();
        _playerHands[loserId].AddRange(cardsToGive);

        var discarded = AutoDiscardSets(loserId);

        // Clear round state
        CardsInPlay.Clear();
        ClaimedRank = null;
        Phase = RoundPhase.WaitingForClaim;
        LastBatchCount = 0;

        // Player after loser starts next round
        int loserIndex = PlayerOrder.IndexOf(loserId);
        if (loserIndex == -1)
        {
            throw new InvalidOperationException("Loser not found");
        }

        CurrentPlayerIndex = (loserIndex + 1) % PlayerOrder.Count;

        return discarded;
    }

    public void NextPlayer()
    {
        if (PlayerOrder.Count == 0)
        {
            return;
        }

        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % PlayerOrder.Count;
    }

    public bool CheckWinCondition()
    {
        foreach (var playerId in PlayerOrder)
        {
            if (_playerHands[playerId].Count == 0)
            {
                WinnerId = playerId;
                return true;
            }
        }

        return false;
    }

    public List<Card> GetPlayerHand(string playerId)
    {
        if (!_playerHands.ContainsKey(playerId))
        {
            return new List<Card>();
        }

        return new List<Card>(_playerHands[playerId]);
    }

    public int GetPlayerHandCount(string playerId)
    {
        if (!_playerHands.ContainsKey(playerId))
        {
            return 0;
        }

        return _playerHands[playerId].Count;
    }

    public Dictionary<string, int> GetAllPlayerHandCounts()
    {
        var counts = new Dictionary<string, int>();
        foreach (var playerId in PlayerOrder)
        {
            counts[playerId] = GetPlayerHandCount(playerId);
        }

        return counts;
    }

    public Dictionary<string, int> GetCardsInPlayCounts()
    {
        var counts = new Dictionary<string, int>();
        foreach (var card in CardsInPlay)
        {
            if (!counts.ContainsKey(card.PlayerId))
            {
                counts[card.PlayerId] = 0;
            }
            counts[card.PlayerId]++;
        }

        return counts;
    }
}