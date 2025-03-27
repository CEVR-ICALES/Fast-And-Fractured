namespace Enums
{
    public enum BRAKE_MODE
    {
        AllWheels,
        FrontWheels,
        RearWheel,
        FrontWheelsStronger,
        RearWheelsStronger
    }

    public enum INPUT_DEVICE_TYPE
    {
        KeyboardMouse,
        PSController,
        XboxController
    }

    public enum STEERING_MODE
    {
        FrontWheel,
        RearWheel,
        AllWheel
    }

    public enum UIElementType
    {
        HealthBar,
        DashCooldown,
        UltCooldown,
        PushCooldown,
        ShootCooldown,
        EventText,
        TimerText,
        DashIcon,
        UltIcon,
        PushIcon,
        ShootIcon,
        DashBinding,
        UltBinding,
        PushBinding,
        ShootBinding,
        Player0,
        Player1,
        Player2,
        Player3,
        Player4,
        Player5,
        Player6,
        Player7,
        BadEffects,
        Effects,
        GoodEffects
    }

    public enum ScreensType
    {
        MAIN_MENU,
        SETTINGS,
        CREDITS,
        GAMEMODE_SELECTION,
        CHARACTER_SELECTION,
        LOADING,
        SPLASH_SCREEN
    }

    public enum Pooltype
    {
        BULLET,
        INTERACTOR,
        NORMAL_BULLET,
        PUSH_BULLET
    }

    public enum PathMode
    {
        SIMPLE,
        ADVANCED
    }

    public enum CollisionType
    {
        COLLISION,
        TRIGGER
    }

    public enum InputBlockTypes 
    {
        ALL_MECHANICS,
        MOVEMENT_MECHANICS,
        SHOOTING_MECHANICS
    }

    public enum STATS
    {
        MAX_SPEED,
        ACCELERATION,
        ENDURANCE,
        NORMAL_DAMAGE,
        PUSH_DAMAGE,
        COOLDOWN_SPEED
    }

    public enum TimerDirection
    {
        Increase,
        Decrease
    }
}