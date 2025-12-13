using System.Diagnostics.CodeAnalysis;
using GameEngine.Messaging;

namespace SocketClient;

public partial class GameWindowForm : Form
{
    private GameClient _client = new();

    // Controls
    private Panel pnlLogin;
    private TextBox txtName;
    private Button btnConnect;

    private Panel pnlLobbySelection;
    private Button btnCreateLobby;
    private TextBox txtJoinCode;
    private Button btnJoinLobby;

    private Panel pnlGameRoom;
    private Label lblLobbyInfo;
    private ListBox lstPlayers;
    private Button btnStartGame;
    private ListBox lstLobbyLog;
    private ListBox lstGameRoomLog;
    private ListBox lstGameLog;

    // Card game controls
    private Panel pnlCardGame;
    private FlowLayoutPanel pnlHand;
    private Label lblRoundInfo;
    private Label lblCurrentPlayer;
    private Button btnClaimRank;
    private ComboBox cmbRank;
    private Button btnPutDownCards;
    private Button btnCallBluff;
    private ComboBox cmbBluffCard;
    private Label lblPlayerCounts;
    private ListBox lstCardsInPlay;

    // Game state
    private GameStateUpdate? _currentGameState;
    private List<CardInfo> _ownHand = new();
    private Dictionary<string, int> _playerHandCounts = new();
    private Dictionary<string, int> _cardsInPlayCounts = new();
    private int? _claimedRank;
    private string _currentPhase = "";
    private bool _gameStateReceived = false;
    private int _lastBatchCount = 0;

    public GameWindowForm()
    {
        InitializeComponent();
        SetupUI();
        SetupEvents();
    }

    private void SetupUI()
    {
        Text = "Socket Game Client";
        Size = new Size(600, 500);

        // Login Panel
        pnlLogin = new Panel { Dock = DockStyle.Fill };
        var lblName = new Label { Text = "Enter Name:", Top = 50, Left = 50 };
        txtName = new TextBox { Top = 50, Left = 150, Width = 200 };
        btnConnect = new Button { Text = "Connect", Top = 80, Left = 150, Height = 30 };
        pnlLogin.Controls.AddRange(new Control[] { lblName, txtName, btnConnect });

        // Lobby Selection Panel
        pnlLobbySelection = new Panel { Dock = DockStyle.Fill, Visible = false };
        btnCreateLobby = new Button { Text = "Create Lobby", Top = 50, Left = 50, Width = 150, Height = 30 };
        var lblJoin = new Label { Text = "Or Join Code:", Top = 100, Left = 50 };
        txtJoinCode = new TextBox { Top = 100, Left = 150, Width = 100 };
        btnJoinLobby = new Button { Text = "Join", Top = 130, Left = 150, Height = 30 };
        var lblLobbyLogLabel = new Label { Text = "Log:", Top = 160, Left = 50 };
        lstLobbyLog = new ListBox { Top = 190, Left = 50, Width = 500, Height = 200 };
        pnlLobbySelection.Controls.AddRange(new Control[]
            { btnCreateLobby, lblJoin, txtJoinCode, btnJoinLobby, lblLobbyLogLabel, lstLobbyLog });

        // Game Room Panel
        pnlGameRoom = new Panel { Dock = DockStyle.Fill, Visible = false };
        lblLobbyInfo = new Label { Top = 10, Left = 10, Width = 200, Text = "Lobby: " };
        lstPlayers = new ListBox { Top = 40, Left = 10, Width = 200, Height = 100 };
        btnStartGame = new Button { Text = "Start Game", Top = 150, Left = 10, Width = 120, Height = 30 };
        var btnLeaveLobby = new Button { Text = "Leave Lobby", Top = 190, Left = 10, Width = 120, Height = 30 };
        var lblGameRoomLog = new Label { Text = "Log:", Top = 10, Left = 220 };
        lstGameRoomLog = new ListBox { Top = 40, Left = 220, Width = 350, Height = 300 };
        pnlGameRoom.Controls.AddRange(new Control[]
            { lblLobbyInfo, lstPlayers, btnStartGame, btnLeaveLobby, lblGameRoomLog, lstGameRoomLog });

        // Card Game Panel
        pnlCardGame = new Panel { Dock = DockStyle.Fill, Visible = false };
        SetupCardGameUI();

        Controls.Add(pnlLogin);
        Controls.Add(pnlLobbySelection);
        Controls.Add(pnlGameRoom);
        Controls.Add(pnlCardGame);
    }

    private void SetupCardGameUI()
    {
        Size = new Size(1000, 700);

        // Round info
        lblRoundInfo = new Label
            { Top = 10, Left = 10, Width = 400, Height = 30, Font = new Font("Arial", 12, FontStyle.Bold) };
        lblCurrentPlayer = new Label { Top = 40, Left = 10, Width = 400, Height = 30 };

        // Player counts
        lblPlayerCounts = new Label { Top = 80, Left = 10, Width = 200, Height = 100 };

        // Hand display
        var lblHand = new Label { Text = "Your Hand:", Top = 200, Left = 10 };
        pnlHand = new FlowLayoutPanel
        {
            Top = 230, Left = 10, Width = 480, Height = 150, AutoScroll = true, BorderStyle = BorderStyle.FixedSingle
        };

        // Rank claim
        var lblClaim = new Label { Text = "Claim Rank (Round Start):", Top = 400, Left = 10 };
        cmbRank = new ComboBox
            { Top = 400, Left = 180, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Height = 30 };
        cmbRank.Items.AddRange(new object[] { "6", "7", "8", "9", "10", "J", "Q", "K" });
        btnClaimRank = new Button { Text = "Claim Rank", Top = 400, Left = 290, Width = 100, Height = 30 };

        // Put down cards
        btnPutDownCards = new Button
            { Text = "Put Down Selected Cards (1-3)", Top = 440, Left = 10, Width = 250, Height = 30 };

        // Call bluff
        var lblBluff = new Label { Text = "Call Bluff - Check Card #:", Top = 480, Left = 10 };
        cmbBluffCard = new ComboBox
            { Top = 480, Left = 180, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Height = 30 };
        btnCallBluff = new Button { Text = "Call Bluff", Top = 480, Left = 290, Width = 100, Height = 30 };

        // Cards in play
        var lblInPlay = new Label { Text = "Cards in Play:", Top = 200, Left = 500 };
        lstCardsInPlay = new ListBox { Top = 230, Left = 500, Width = 200, Height = 150 };

        // Game log
        var lblLog = new Label { Text = "Game Log:", Top = 400, Left = 500 };
        lstGameLog = new ListBox { Top = 430, Left = 500, Width = 460, Height = 200 };

        // Leave lobby button
        var btnLeaveLobbyGame = new Button { Text = "Leave Lobby", Top = 520, Left = 10, Width = 120, Height = 30 };

        pnlCardGame.Controls.AddRange(new Control[]
        {
            lblRoundInfo, lblCurrentPlayer, lblPlayerCounts,
            lblHand, pnlHand,
            lblClaim, cmbRank, btnClaimRank,
            btnPutDownCards,
            lblBluff, cmbBluffCard, btnCallBluff,
            lblInPlay, lstCardsInPlay,
            lblLog, lstGameLog,
            btnLeaveLobbyGame
        });
    }

    private void SetupEvents()
    {
        btnConnect.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                return;
            }

            btnConnect.Enabled = false;
            try
            {
                await _client.ConnectAndLogin("127.0.0.1", 5000, txtName.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Connection failed: " + ex.Message);
                btnConnect.Enabled = true;
            }
        };

        btnCreateLobby.Click += async (_, _) => await _client.CreateLobby();
        btnJoinLobby.Click += async (_, _) => await _client.JoinLobby(txtJoinCode.Text);
        btnStartGame.Click += async (_, _) => await _client.StartGame();

        // Leave lobby buttons
        var btnLeaveLobby = pnlGameRoom.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "Leave Lobby");
        if (btnLeaveLobby != null)
        {
            btnLeaveLobby.Click += async (_, _) => await _client.LeaveLobby();
        }

        var btnLeaveLobbyGame = pnlCardGame.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "Leave Lobby");
        if (btnLeaveLobbyGame != null)
        {
            btnLeaveLobbyGame.Click += async (_, _) => await _client.LeaveLobby();
        }

        _client.OnLog += msg => Log(msg);

        _client.OnLobbyCreated += (lobbyResponse) =>
        {
            Invoke(() =>
            {
                lstGameRoomLog.Items.Clear();
                _lobbyCode = lobbyResponse.LobbyCode;
                _ownerId = lobbyResponse.OwnerId;
                lblLobbyInfo.Text = $"Lobby Code: {_lobbyCode}";
                SwitchToPanel(pnlGameRoom);
                Log($"Lobby Created: {_lobbyCode}");
            });
        };

        _client.OnLobbyUpdate += update =>
        {
            Invoke(() =>
            {
                // Switch to game room if we just joined
                if (pnlLobbySelection.Visible)
                {
                    lstGameRoomLog.Items.Clear();
                    SwitchToPanel(pnlGameRoom);
                }

                // Track previous player list to detect joins/leaves
                var previousPlayers = new HashSet<string>();
                foreach (string player in lstPlayers.Items)
                {
                    previousPlayers.Add(player);
                }

                var currentPlayers = new HashSet<string>(update.PlayerNames);

                // Detect player joins
                foreach (var player in currentPlayers)
                {
                    if (!previousPlayers.Contains(player))
                    {
                        Log($"Player joined: {player}");
                    }
                }

                // Detect player leaves
                foreach (var player in previousPlayers)
                {
                    if (!currentPlayers.Contains(player))
                    {
                        Log($"Player left: {player}");
                    }
                }

                lblLobbyInfo.Text = $"Lobby Code: {_lobbyCode}";
                lstPlayers.Items.Clear();
                foreach (var p in update.PlayerNames)
                {
                    lstPlayers.Items.Add(p);
                }

                btnStartGame.Enabled = _ownerId == _client.PlayerId;
            });
        };

        _client.OnGameStarted += (evt) =>
        {
            Invoke(() =>
            {
                lstGameLog.Items.Clear();
                // Reset game state flag for new game
                _gameStateReceived = false;
                _gameEnded = false;
                _playerIdToName.Clear();

                // Store playerId->name mapping from both lists
                for (int i = 0; i < Math.Min(evt.PlayerIds.Count, evt.PlayerNames.Count); i++)
                {
                    _playerIdToName[evt.PlayerIds[i]] = evt.PlayerNames[i];
                }

                btnStartGame.Enabled = false;
                SwitchToPanel(pnlCardGame);
            });
        };

        // Game events
        _client.OnGameStateUpdate += (state) =>
        {
            Invoke(() =>
            {
                if (!_gameStateReceived)
                {
                    Log("Game Started!");
                    _gameStateReceived = true;
                }

                UpdateGameState(state);
            });
        };

        _client.OnCardsPutDown += (evt) =>
        {
            var playerName = _playerIdToName.TryGetValue(evt.PlayerId, out var name) ? name : evt.PlayerId;
            Invoke(() => Log($"{playerName} put down {evt.CardCount} card(s)"));
        };

        _client.OnBluffCalled += (evt) =>
        {
            var callerName = _playerIdToName.TryGetValue(evt.CallerId, out var cname) ? cname : evt.CallerId;
            var loserName = _playerIdToName.TryGetValue(evt.LoserId, out var lname) ? lname : evt.LoserId;
            string rankStr = GetRankString(evt.RevealedCard.Rank);
            string result = evt.IsBluff ? "BLUFF!" : "TRUTH";
            Invoke(() =>
                Log(
                    $"{callerName} called bluff on card #{evt.CardIndex}. Revealed: {rankStr} ({result}). {loserName} loses!"));
        };

        _client.OnRoundStarted += (evt) =>
        {
            var playerName = _playerIdToName.TryGetValue(evt.StartingPlayerId, out var name)
                ? name
                : evt.StartingPlayerId;
            string rankStr = GetRankString((int)evt.ClaimedRank);
            Invoke(() => Log($"Round started! {playerName} claimed {rankStr}"));
        };

        _client.OnRoundEnded += (evt) =>
        {
            var loserName = _playerIdToName.TryGetValue(evt.LoserId, out var lname) ? lname : evt.LoserId;
            var nextStarterName = _playerIdToName.TryGetValue(evt.NextRoundStarterId, out var sname)
                ? sname
                : evt.NextRoundStarterId;

            Invoke(() =>
            {
                Log(
                    $"Round ended! {loserName} picked up {evt.TotalCardsPickedUp} cards. Next round starts with {nextStarterName}");

                if (evt.DiscardedRanks.Count > 0)
                {
                    var rankStrings = evt.DiscardedRanks.Select(r => GetRankString(r)).ToList();
                    Log($"{loserName} discarded: {string.Join(", ", rankStrings)}");
                }
            });
        };

        _client.OnGameEnded += (evt) =>
        {
            Invoke(() =>
            {
                if (_gameEnded)
                {
                    return;
                }
                _gameEnded = true;

                string message;
                if (string.IsNullOrEmpty(evt.WinnerId))
                {
                    message = "Game ended: player left";
                }
                else
                {
                    var winnerName = _playerIdToName.TryGetValue(evt.WinnerId, out var name) ? name : evt.WinnerId;
                    message = $"{winnerName} wins the game!";
                }

                Log($"*** GAME OVER! {message} ***");
                MessageBox.Show(message, "Game Over", MessageBoxButtons.OK, MessageBoxIcon.Information);

                btnClaimRank.Enabled = false;
                btnPutDownCards.Enabled = false;
                btnCallBluff.Enabled = false;
            });
        };

        _client.OnLeftLobby += () => { Invoke(() => { SwitchToPanel(pnlLobbySelection); }); };

        _client.OnConnected += () => { Invoke(() => SwitchToPanel(pnlLobbySelection)); };

        // Card game button handlers
        btnClaimRank.Click += async (_, _) =>
        {
            if (_gameEnded || cmbRank.SelectedIndex == -1)
            {
                return;
            }

            int rank = cmbRank.SelectedIndex + 6; // 6-13 (six through king)
            await _client.ClaimRank(rank);
        };

        btnPutDownCards.Click += async (_, _) =>
        {
            if (_gameEnded)
            {
                return;
            }

            var selected = GetSelectedCardIndices();
            if (selected.Count < 1 || selected.Count > 3)
            {
                MessageBox.Show("Please select 1-3 cards to put down.", "Invalid Selection", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            await _client.PutDownCards(selected);
        };

        btnCallBluff.Click += async (_, _) =>
        {
            if (_gameEnded || cmbBluffCard.SelectedIndex == -1 || cmbBluffCard.SelectedItem == null)
            {
                return;
            }

            int cardNumber = (int)cmbBluffCard.SelectedItem;
            await _client.CallBluff(cardNumber);
        };
    }

    private void UpdateGameState(GameStateUpdate state)
    {
        _currentGameState = state;
        _ownHand = state.OwnHand;
        _playerHandCounts = state.PlayerHandCounts;
        _cardsInPlayCounts = state.CardsInPlayCounts;
        _claimedRank = state.ClaimedRank;
        _currentPhase = state.Phase;
        _lastBatchCount = state.LastBatchCount;

        // Update round info
        if (_claimedRank.HasValue)
        {
            lblRoundInfo.Text = $"Round: Claimed Rank = {GetRankString(_claimedRank.Value)}";
        }
        else
        {
            lblRoundInfo.Text = "Round: Waiting for rank claim";
        }

        // Update current player
        var currentPlayerName = _playerIdToName.TryGetValue(state.CurrentPlayerId, out var currentName)
            ? currentName
            : state.CurrentPlayerId;
        lblCurrentPlayer.Text = $"Current Player: {currentPlayerName}";
        if (state.CurrentPlayerId == _client.PlayerId)
        {
            lblCurrentPlayer.Text += " (YOU)";
            lblCurrentPlayer.ForeColor = Color.Green;
        }
        else
        {
            lblCurrentPlayer.ForeColor = Color.Black;
        }

        // Update player counts with player order info
        var countsText = "Players (in order):\n";
        foreach (var kvp in _playerHandCounts)
        {
            string playerName;
            if (_playerIdToName.TryGetValue(kvp.Key, out var playerNameValue))
            {
                playerName = playerNameValue;
            }
            else
            {
                playerName = kvp.Key;
            }

            var marker = kvp.Key == state.CurrentPlayerId ? " ←" : "";
            countsText += $"{playerName}: {kvp.Value} cards{marker}\n";
        }

        lblPlayerCounts.Text = countsText;

        if (state.CurrentPlayerId != _client.PlayerId)
        {
            _selectedCardIndices.Clear();
        }

        UpdateHandDisplay();
        UpdateCardsInPlay();
        UpdateUIState();
    }

    private void UpdateHandDisplay()
    {
        pnlHand.Controls.Clear();
        _selectedCardIndices.Clear();

        // Create sorted list with original indices for display
        var sortedCards = _ownHand
            .Select((card, originalIndex) => new { Card = card, OriginalIndex = originalIndex })
            .OrderBy(x => x.Card.Rank)
            .ThenBy(x => x.Card.Suit)
            .ToList();

        for (int displayIndex = 0; displayIndex < sortedCards.Count; displayIndex++)
        {
            var item = sortedCards[displayIndex];
            var card = item.Card;
            var originalIndex = item.OriginalIndex;

            var suitSymbol = GetSuitSymbol(card.Suit);
            var btn = new Button
            {
                Text = $"{GetRankString(card.Rank)}\n{suitSymbol}",
                Width = 60,
                Height = 90,
                Margin = new Padding(5),
                Tag = originalIndex,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btn.FlatAppearance.BorderColor = Color.Gray;
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += (_, _) => ToggleCardSelection(btn);

            pnlHand.Controls.Add(btn);
        }
    }

    private string GetSuitSymbol(int suit)
    {
        return suit switch
        {
            0 => "♠",
            1 => "♥",
            2 => "♦",
            3 => "♣",
            _ => "?"
        };
    }

    private readonly HashSet<int> _selectedCardIndices = new();

    private void ToggleCardSelection(Button btn)
    {
        if (btn.Tag == null)
        {
            return;
        }

        int index = (int)btn.Tag;
        if (_selectedCardIndices.Contains(index))
        {
            _selectedCardIndices.Remove(index);
            btn.BackColor = Color.White;
            btn.FlatAppearance.BorderColor = Color.Gray;
        }
        else
        {
            if (_selectedCardIndices.Count >= 3)
            {
                MessageBox.Show("You can only select up to 3 cards.", "Too Many Cards", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _selectedCardIndices.Add(index);
            btn.BackColor = Color.LightBlue;
            btn.FlatAppearance.BorderColor = Color.Blue;
            btn.FlatAppearance.BorderSize = 2;
        }
    }

    private List<int> GetSelectedCardIndices()
    {
        return _selectedCardIndices.OrderBy(x => x).ToList();
    }

    private void UpdateCardsInPlay()
    {
        lstCardsInPlay.Items.Clear();
        int totalCards = 0;
        foreach (var kvp in _cardsInPlayCounts)
        {
            var playerName = _playerIdToName.TryGetValue(kvp.Key, out var name) ? name : kvp.Key;
            lstCardsInPlay.Items.Add($"{playerName}: {kvp.Value} card(s)");
            totalCards += kvp.Value;
        }

        if (totalCards > 0)
        {
            lstCardsInPlay.Items.Add($"--- Total: {totalCards} ---");
        }

        // Update bluff card combo
        cmbBluffCard.Items.Clear();
        if (_lastBatchCount > 0)
        {
            for (int i = 1; i <= _lastBatchCount; i++)
            {
                cmbBluffCard.Items.Add(i);
            }
        }
    }

    private void UpdateUIState()
    {
        if (_gameEnded)
        {
            btnClaimRank.Enabled = false;
            cmbRank.Enabled = false;
            btnPutDownCards.Enabled = false;
            btnCallBluff.Enabled = false;
            cmbBluffCard.Enabled = false;
            return;
        }

        bool isMyTurn = _currentGameState?.CurrentPlayerId == _client.PlayerId;
        bool isWaitingForClaim = _currentPhase == "WaitingForClaim";
        bool isCheckingBluff = _currentPhase == "CheckingBluff";

        // Rank claim
        btnClaimRank.Enabled = isMyTurn && isWaitingForClaim;
        cmbRank.Enabled = isMyTurn && isWaitingForClaim;

        // Put down cards
        btnPutDownCards.Enabled = isMyTurn && !isWaitingForClaim && !isCheckingBluff;

        // Call bluff
        bool canCallBluff = isMyTurn && (_currentPhase == "Playing" || isCheckingBluff);
        btnCallBluff.Enabled = canCallBluff && cmbBluffCard.Items.Count > 0;
        cmbBluffCard.Enabled = canCallBluff;
    }

    private string GetRankString(int rank)
    {
        return rank switch
        {
            6 => "6",
            7 => "7",
            8 => "8",
            9 => "9",
            10 => "10",
            11 => "J",
            12 => "Q",
            13 => "K",
            14 => "A",
            _ => rank.ToString()
        };
    }

    private Dictionary<string, string> _playerIdToName = new();
    private string _lobbyCode = string.Empty;
    private string _ownerId = string.Empty;
    private bool _gameEnded = false;

    private void Log(string msg)
    {
        if (InvokeRequired)
        {
            Invoke(() => Log(msg));
        }
        else
        {
            // Determine which listbox to use based on visible panel
            ListBox? targetListBox = null;
            if (pnlLobbySelection.Visible)
            {
                targetListBox = lstLobbyLog;
            }
            else if (pnlGameRoom.Visible)
            {
                targetListBox = lstGameRoomLog;
            }
            else if (pnlCardGame.Visible)
            {
                targetListBox = lstGameLog;
            }

            if (targetListBox == null)
            {
                targetListBox = lstLobbyLog;
            }

            targetListBox.Items.Add(msg);
            targetListBox.TopIndex = targetListBox.Items.Count - 1;
        }
    }

    private void SwitchToPanel(Panel panel)
    {
        pnlLogin.Visible = false;
        pnlLobbySelection.Visible = false;
        pnlGameRoom.Visible = false;
        pnlCardGame.Visible = false;
        panel.Visible = true;
    }
}