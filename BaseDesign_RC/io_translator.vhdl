-- FPGA RemoCon Project: translation between I/O and command string
-- 2023.07.06 Naoki F., AIT
------------------------------------------------------------------------
library IEEE;
use IEEE.std_logic_1164.ALL;
use IEEE.std_logic_unsigned.ALL;

entity io_translator is
    generic ( SAMPLE_INT : integer := 1000000; -- sampling interval of 7-seg output
              STABLE_INT : integer := 16);     -- cycles to see if 7-seg input is stable
    port ( CLK, RST  : in  std_logic;
           BOARD_SW  : in  std_logic_vector(12 downto 0);   -- switch input of board
           BOARD_LED : out std_logic_vector( 7 downto 0);   -- LED output of board
           BOARD_AN  : out std_logic_vector( 3 downto 0);   -- 7-seg anode of board
           BOARD_SEG : out std_logic_vector( 7 downto 0);   -- 7-seg segment of board
           USER_SW   : out std_logic_vector(12 downto 0);   -- switch input of user circuit
           USER_LED  : in  std_logic_vector( 7 downto 0);   -- LED output of user circuit
           USER_AN   : in  std_logic_vector( 3 downto 0);   -- 7-seg anode of user circuit
           USER_SEG  : in  std_logic_vector( 7 downto 0);   -- 7-seg segment of user circuit
           RST_REG   : in  std_logic;                       -- set '1' to reset user_*_reg
           DATA_OUT  : out std_logic_vector( 7 downto 0);   -- char to be sent with UART
           WE        : out std_logic;                       -- set '1' when DATA_OUT is valid
           FULL      : in  std_logic;                       -- sent char will be ignored if '1'
           DATA_IN   : in  std_logic_vector( 7 downto 0);   -- char received with UART
           RE        : out std_logic;                       -- set '1' when accepting DATA_IN
           EMPTY     : in  std_logic);                      -- DATA_IN is not valid if '1'
end io_translator;

architecture RTL of io_translator is
    component io_7segreg is
        generic ( SAMPLE_INT : integer := 1000000;
                  STABLE_INT : integer := 16);
        port ( CLK, RST : in  std_logic;
               AN       : in  std_logic_vector( 3 downto 0);
               SEG      : in  std_logic_vector( 7 downto 0);
               LED_OUT  : out std_logic_vector(31 downto 0));
    end component;

    component io_ledreg is
        generic ( SAMPLE_INT : integer := 200000;
                  STABLE_INT : integer := 16);
        port ( CLK, RST : in  std_logic;
               LED_IN   : in  std_logic_vector( 7 downto 0);
               LED_OUT  : out std_logic_vector( 7 downto 0));
    end component;

    signal recv_first_char : std_logic;                     -- '1' after receiving 1st char
    signal user_sw_reg     : std_logic_vector(12 downto 0); -- stored USER_SW value
    signal user_sw_sel     : integer range 0 to 12;         -- index of selected SW
    signal sent_led_char   : std_logic;                     -- '1' when LED group select is sent
    signal next_led_char   : std_logic_vector( 7 downto 0); -- char to be sent for turning LED on/off
    signal user_led_val    : std_logic_vector(39 downto 0); -- concatenation of LED and 7-seg values
    signal user_led_reg    : std_logic_vector(39 downto 0); -- stored USER_LED value
    signal user_led_sel    : integer range 0 to 4;          -- index of selected LED group
    signal user_led_pos    : integer range 0 to 7;          -- index of LED in selected LED group
    signal user_led_new    : std_logic;                     -- new value of selected LED
begin

    -- instantiation of sampler circuits
    seg : io_7segreg
        generic map (SAMPLE_INT, STABLE_INT)
        port map (CLK, RST, USER_AN, USER_SEG, user_led_val(31 downto 0));
    led : io_ledreg
        generic map (SAMPLE_INT / 5, STABLE_INT)
        port map (CLK, RST, USER_LED, user_led_val(39 downto 32));

    -- board LED signals are the same as user LED signals
    BOARD_LED <= USER_LED;
    BOARD_AN  <= USER_AN;
    BOARD_SEG <= USER_SEG;

    -- RE is '1' when DATA_IN is valid
    RE <= not EMPTY;

    -- USER_SW is BOARD_SW before receiving first char, and managed by this circuit after that
    USER_SW <= BOARD_SW when recv_first_char = '0' else user_sw_reg;

    -- turn USER_SW on/off according to command strings
    process (CLK) begin
        if rising_edge(CLK) then
            if RST = '1' then
                recv_first_char <= '0';
                user_sw_reg <= (others => '0');
                user_sw_sel <= 0;
            elsif RST_REG = '1' then
                user_sw_reg <= (others => '0');
            elsif EMPTY = '0' then
                recv_first_char <= '1';
                case DATA_IN is
                    -- when received char is I-S, select corresponding SW
                    when x"49" => user_sw_sel <= 0;  -- I
                    when x"4a" => user_sw_sel <= 1;  -- J
                    when x"4b" => user_sw_sel <= 2;  -- K
                    when x"4c" => user_sw_sel <= 3;  -- L
                    when x"4d" => user_sw_sel <= 4;  -- M
                    when x"4e" => user_sw_sel <= 5;  -- N
                    when x"4f" => user_sw_sel <= 6;  -- O
                    when x"50" => user_sw_sel <= 7;  -- P
                    when x"51" => user_sw_sel <= 8;  -- Q
                    when x"52" => user_sw_sel <= 9;  -- R
                    when x"53" => user_sw_sel <= 10; -- S
                    when x"71" => user_sw_sel <= 11; -- q
                    when x"72" => user_sw_sel <= 12; -- r
                    -- when large U, turn the selected SW on
                    when x"55" => user_sw_reg(user_sw_sel) <= '1';
                    -- when small u, turn the selected SW off
                    when x"75" => user_sw_reg(user_sw_sel) <= '0';
                    when others => null;
                end case;
            end if;
        end if;
    end process;

    -- detect change of the value of USER_LED
    process (user_led_val, user_led_reg)
        variable user_led_comp             : std_logic_vector(39 downto 0);
        variable sel_led_val, sel_led_comp : std_logic_vector( 7 downto 0);
    begin
        user_led_sel  <= 0;
        user_led_pos  <= 0;
        user_led_new  <= '0';
        -- determine LED group to be selected
        user_led_comp := user_led_val xor user_led_reg;
        sel_led_val   := user_led_val (7 downto 0);
        sel_led_comp  := user_led_comp(7 downto 0);
        for i in 0 to 4 loop
            if (user_led_comp((i * 8 + 7) downto (i * 8)) /= x"00") then
                user_led_sel  <= i;
                sel_led_val   := user_led_val ((i * 8 + 7) downto (i * 8));
                sel_led_comp  := user_led_comp((i * 8 + 7) downto (i * 8));
                exit;
            end if;
        end loop;
        -- determine changed LED in the selected group
        for i in 0 to 7 loop
            if (sel_led_comp(i) = '1') then
                user_led_pos <= i;
                user_led_new <= sel_led_val(i);
                exit;
            end if;
        end loop;
    end process;

    -- if LED value has been changed, send a corresponding command string
    process (user_led_val, user_led_reg, sent_led_char, next_led_char, user_led_sel) begin
        if user_led_val /= user_led_reg and sent_led_char = '0' then
            WE       <= '1';
            case user_led_sel is
                when 0 => DATA_OUT <= x"30"; -- 0
                when 1 => DATA_OUT <= x"31"; -- 1
                when 2 => DATA_OUT <= x"32"; -- 2
                when 3 => DATA_OUT <= x"33"; -- 3
                when 4 => DATA_OUT <= x"34"; -- 4
            end case;
        elsif sent_led_char = '1' then
            WE       <= '1';
            DATA_OUT <= next_led_char;
        else
            WE       <= '0';
            DATA_OUT <= x"00";
        end if;
    end process;

    -- update stored USER_LED value on sending a command string
    process (CLK) begin
        if rising_edge(CLK) then
            if RST = '1' then
                sent_led_char <= '0';
                next_led_char <= x"00";
                user_led_reg  <= (others => '0');
            elsif RST_REG = '1' then
                -- reset register to inverse of current value, to force resend commands
                user_led_reg  <= not user_led_val;
            elsif user_led_val /= user_led_reg and sent_led_char = '0' and FULL = '0' then
                sent_led_char <= '1';
                -- set the next char to be sent
                if user_led_new = '1' then
                    case user_led_pos is
                        when 0 => next_led_char <= x"41"; -- 'A'
                        when 1 => next_led_char <= x"42"; -- 'B'
                        when 2 => next_led_char <= x"43"; -- 'C'
                        when 3 => next_led_char <= x"44"; -- 'D'
                        when 4 => next_led_char <= x"45"; -- 'E'
                        when 5 => next_led_char <= x"46"; -- 'F'
                        when 6 => next_led_char <= x"47"; -- 'G'
                        when 7 => next_led_char <= x"48"; -- 'H'
                    end case;
                else
                    case user_led_pos is
                        when 0 => next_led_char <= x"61"; -- 'a'
                        when 1 => next_led_char <= x"62"; -- 'b'
                        when 2 => next_led_char <= x"63"; -- 'c'
                        when 3 => next_led_char <= x"64"; -- 'd'
                        when 4 => next_led_char <= x"65"; -- 'e'
                        when 5 => next_led_char <= x"66"; -- 'f'
                        when 6 => next_led_char <= x"67"; -- 'g'
                        when 7 => next_led_char <= x"68"; -- 'h'
                    end case;
                end if;
                -- update stored USER_LED value
                user_led_reg(user_led_sel * 8 + user_led_pos) <= user_led_new;
            elsif sent_led_char = '1' and FULL = '0' then
                sent_led_char <= '0';
            end if;
        end if;
    end process;
end RTL;
