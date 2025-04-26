namespace Enums
{
    public enum BrakeMode
    {
        ALL_WHEELS,
        FRONT_WHEELS,
        REAR_WHEEL,
        FRONT_WHEELS_STRONGER,
        REAR_WHEELS_STRONGER
    }

    public enum InputDeviceType
    {
        KEYBOARD_MOUSE,
        PS_CONTROLLER,
        XBOX_CONTROLLER
    }

    public enum SteeringMode
    {
        FRONT_WHEEL,
        REAR_WHEEL,
        ALL_WHEEL
    }

    public enum UIElementType
    {
        HEALTH_BAR,
        DASH_COOLDOWN,
        ULT_COOLDOWN,
        PUSH_COOLDOWN,
        SHOOT_COOLDOWN,
        EVENT_TEXT,
        TIMER_TEXT,
        DASH_ICON,
        ULT_ICON,
        PUSH_ICON,
        SHOOT_ICON,
        DASH_BINDING,
        ULT_BINDING,
        PUSH_BINDING,
        SHOOT_BINDING,
        PLAYER_0,
        PLAYER_1,
        PLAYER_2,
        PLAYER_3,
        PLAYER_4,
        PLAYER_5,
        PLAYER_6,
        PLAYER_7,
        BAD_EFFECTS,
        NORMAL_EFFECTS,
        GOOD_EFFECTS,
        SHOOTING_CROSSHAIR
    }

    public enum ScreensType
    {
        MAIN_MENU,
        SETTINGS,
        CREDITS,
        GAMEMODE_SELECTION,
        CHARACTER_SELECTION,
        LOADING,
        SPLASH_SCREEN,
        EXIT_POPUP,
        PAUSE,
        WIN_LOSE
    }

    public enum Pooltype
    {
        BULLET,
        INTERACTOR,
        NORMAL_BULLET,
        PUSH_BULLET,
        ASCENDING_TOMATO,
        DESCENDING_TOMATO,
        NORMAL_BULLET_EGG,
        PUSH_BULLET_CHICKEN,
        NORMAL_BULLET_GOLF,
        PUSH_BULLET_GOLF,
        NORMAL_BULLET_TOMATO,
        PUSH_BULLET_MELON,
        NORMAL_BULLET_CROQUETA,
        PUSH_BULLET_SARTENAZO
    }

    public enum PathMode
    {
        SIMPLE,
        ADVANCED
    }

    public enum IAGroundState
    {
        GROUND,
        AIR,
        FLIP_SATE,
        NONE
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

    public enum Stats
    {
        MAX_SPEED,
        ACCELERATION,
        ENDURANCE,
        NORMAL_DAMAGE,
        PUSH_FORCE,
        COOLDOWN_SPEED,
        MAX_SPEED_MULTIPLIER
    }

    public enum TimerDirection
    {
        INCREASE,
        DECREASE
    }

    public enum PlayerIcons
    {
        CARME_0,
        JOSEFINO_0,
        PEPE_0,
        MARIAANTONIA_0,
        CARME_1,
        JOSEFINO_1,
        PEPE_1,
        MARIAANTONIA_1
    }

    public enum UniqueAbilitiesIcons
    {
        CARME_UA,
        JOSEFINO_UA,
        PEPE_UA,
        MARIAANTONIA_UA
    }

    public enum ScreenEffects
    {
        SPEED_EFFECT,
        TOMATO_EFFECT
    }
 
    public enum GameElement
    {
        CHARACTER,
        INTERACTABLE
    }
}