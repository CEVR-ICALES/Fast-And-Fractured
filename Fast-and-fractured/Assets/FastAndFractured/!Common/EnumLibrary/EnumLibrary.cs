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

    public enum UIDynamicElementType
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
        PLAYER_ICONS,
        BAD_EFFECTS,
        NORMAL_EFFECTS,
        GOOD_EFFECTS,
        SHOOTING_CROSSHAIR,
        SELECTED_PLAYER_ICON,
        SPEED_INDICATOR,
        EFFECT_ICONS_CONTAINER,
		BULLET_EFFECT
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
        WIN_LOSE,
        HOW_TO_PLAY,
        MAP_SELECTION
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
        SHOOTING_MECHANICS,
        CANCEL_DASH
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
        MARIAANTONIA_1,
        CARME_2,
        JOSEFINO_2,
        PEPE_2,
        MARIAANTONIA_2
    }

    public enum PlayerPortraits
    {
        CARME_0_PORTRAIT,
        JOSEFINO_0_PORTRAIT,
        PEPE_0_PORTRAIT,
        MARIAANTONIA_0_PORTRAIT,
        CARME_1_PORTRAIT,
        JOSEFINO_1_PORTRAIT,
        PEPE_1_PORTRAIT,
        MARIAANTONIA_1_PORTRAIT,
        CARME_2_PORTRAIT,
        JOSEFINO_2_PORTRAIT,
        PEPE_2_PORTRAIT,
        MARIAANTONIA_2_PORTRAIT
    }

    public enum PlayerHalfBody
    {
        CARME_0_HALFBODY,
        JOSEFINO_0_HALFBODY,
        PEPE_0_HALFBODY,
        MARIAANTONIA_0_HALFBODY,
        CARME_1_HALFBODY,
        JOSEFINO_1_HALFBODY,
        PEPE_1_HALFBODY,
        MARIAANTONIA_1_HALFBODY,
        CARME_2_HALFBODY,
        JOSEFINO_2_HALFBODY,
        PEPE_2_HALFBODY,
        MARIAANTONIA_2_HALFBODY
    }

    public enum UniqueAbilitiesIcons
    {
        CARME_UA,
        JOSEFINO_UA,
        PEPE_UA,
        MARIAANTONIA_UA
    }

    public enum PushShootIcons
    {
        CARME_PUSH,
        JOSEFINO_PUSH,
        PEPE_PUSH,
        MARIAANTONIA_PUSH
    }

    public enum NormalShootIcons
    {
        CARME_SHOOT,
        JOSEFINO_SHOOT,
        PEPE_SHOOT,
        MARIAANTONIA_SHOOT
    }

    public enum ScreenEffects
    {
        SPEED_EFFECT,
        TOMATO_EFFECT,
        BROKEN_CRYSTAL,
        BULLET_EFFECT
    }

    public enum GameElement
    {
        CHARACTER,
        INTERACTABLE,
        SAFE_ZONES
    }

    public enum ModifiedCarState
    {
        JOSEFINO_INVULNERABLE,
        SUPER_MARIA_ANTONIA,
        DEFAULT
    }

    public enum ValueNumberType
    {
        DIRECT_VALUE,
        PERCENTAGE,
    }

    public enum AmbienceZoneType
    {
        NONE,
        FOREST,
        MERCADILLO,
        CONSTRUCTION_ZONE
    }
}