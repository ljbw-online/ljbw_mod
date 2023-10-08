namespace Ljbw_common
{
    enum botToModMessage : byte
    {
        model_output,
        supervised_model_resumed,
        model_paused,
        reinforcement_model_resumed,
        reinforcement_learning_resumed,
        reinforcement_learning_paused,
        paint_random,
        paint_colour,
        reset,
        new_area,
        fix,
        clean,
        UNUSED,
        chat_key,
        starting_position,
        current_position,
        set_control,
        weather,
        veh,
        teleport,
        get_heading,
        set_heading,
        action,
        state
    }
    
    enum modToBotMessage : byte
    {
        starting_position = botToModMessage.starting_position,
        current_position = botToModMessage.current_position,
        current_heading = botToModMessage.get_heading,
        state = botToModMessage.state
    }

    class Class
    {
        public const int script_to_mod_array_length = 17;
        public const int mod_to_script_array_length = 29;

        public static string[] weatherNames = new string[] { "blizzard", "clear", "clearing", "clouds", "extrasunny", "foggy", "halloween", "neutral", "overcast", "rain",
        "smog", "snow", "snowlight", "thunder", "xmas" };

        public static string[] vehicleNames = new string[] { "voltic", "voltic2", "ruiner2", "cablecar", "deluxo", "rhino", "kosatka", "apc", "cheburek", "airtug" };
    }

}