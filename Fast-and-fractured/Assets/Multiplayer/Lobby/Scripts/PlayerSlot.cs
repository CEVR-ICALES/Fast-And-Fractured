namespace FastAndFractured.Multiplayer.Lobby
{
    public enum PlayerSlotKind { Human, Ai }

    public readonly struct PlayerSlot
    {
        public readonly PlayerSlotKind Kind;
        public readonly int ClientId;
        public readonly string CharacterCode;

        public bool IsHuman => Kind == PlayerSlotKind.Human;
        public bool IsAi    => Kind == PlayerSlotKind.Ai;

        public PlayerSlot(int clientId, string characterCode)
        {
            Kind = PlayerSlotKind.Human;
            ClientId = clientId;
            CharacterCode = characterCode;
        }

        public static PlayerSlot ForAi(int aiIndex, string characterCode) =>
            new PlayerSlot(aiIndex, characterCode, PlayerSlotKind.Ai);

        private PlayerSlot(int id, string code, PlayerSlotKind kind)
        {
            Kind = kind;
            ClientId = id;
            CharacterCode = code;
        }
    }
}
