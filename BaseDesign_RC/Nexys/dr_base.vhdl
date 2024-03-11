-- DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
-- Copyright (C) 2022 Naoki FUJIEDA. New BSD License is applied.
------------------------------------------------------------------------

library IEEE;
use IEEE.std_logic_1164.ALL;
use IEEE.std_logic_unsigned.ALL;

-- This is a base circuit for reconfigurable module.
entity dr_base is
    port ( CLK, RST                     : in  std_logic;
           SW                           : in  std_logic_vector(15 downto 0);
           BTNC, BTNL, BTNR, BTNU, BTND : in  std_logic;
           LD                           : out std_logic_vector(15 downto 0);
           AN, SEG                      : out std_logic_vector(7 downto 0));
end dr_base;

architecture RTL of dr_base is
    signal disp_targ, n_disp_targ : std_logic;                     -- '0': LED, '1': 7-seg
    signal refl_mode, n_refl_mode : std_logic_vector(1 downto 0);  -- whether the light pass through edges
    signal light_pos, n_light_pos : std_logic_vector(3 downto 0);  -- position of LED/7-seg to be lit
    signal direction, n_direction : std_logic;                     -- '0': to left, '1': to right
    signal count, n_count         : std_logic_vector(23 downto 0); -- for 100-ms timer
    signal move_en                : std_logic;

begin
    -- 100-ms timer
    n_count <= count + '1' when move_en = '0'    else (others => '0');
    move_en <= '0'         when count /= 9999999 else '1';

    -- LED output (active high)
    process (disp_targ, light_pos, SW) begin
        for i in 0 to 15 loop
            if disp_targ = '0' and light_pos = i then
                LD(i) <= not SW(i);
            else
                LD(i) <= SW(i);
            end if;
        end loop;
    end process;

    -- 7-seg anode output (active low)
    process (disp_targ, light_pos, count) begin
        for i in 0 to 7 loop
            if disp_targ = '1' and light_pos(3 downto 1) = i and count(19 downto 18) = "00" then
                AN(i) <= '0';
            else
                AN(i) <= '1';
            end if;
        end loop;
    end process;

    -- 7-seg segment output (active low)
    SEG     <= "10011110" when light_pos(0) = '0' and direction = '0' else
               "01100001" when light_pos(0) = '1' and direction = '0' else
               "00001100" when light_pos(0) = '0' and direction = '1' else
               "11110011";--   light_pos(0) = '1' and direction = '1'

    -- next mode (decided by the buttons)
    n_disp_targ <= '0'  when BTND = '1' else
                   '1'  when BTNU = '1' else disp_targ;
    n_refl_mode <= "00" when BTNC = '1' else
                   "10" when BTNL = '1' else
                   "01" when BTNR = '1' else refl_mode;
    
    -- next light position
    process (refl_mode, light_pos, direction) begin
        n_direction <= direction;
        if (direction = '0') then
            n_light_pos <= light_pos + '1';
            if (light_pos = 14 and refl_mode(1) = '0') then
                n_direction <= '1';
            end if;
        else
            n_light_pos <= light_pos - '1';
            if (light_pos = 1  and refl_mode(0) = '0') then
                n_direction <= '0';
            end if;
        end if;
    end process;

    -- update registers
    process (CLK) begin
        if (rising_edge(CLK)) then
            if (RST = '1') then
                disp_targ <= '0';
                refl_mode <= "00";
                light_pos <= "0000";
                direction <= '0';
                count     <= (others => '0');
            else
                if (move_en = '1') then
                    disp_targ <= n_disp_targ;
                    refl_mode <= n_refl_mode;
                    light_pos <= n_light_pos;
                    direction <= n_direction;
                end if;
                count     <= n_count;
            end if;
        end if;
    end process;
end RTL;
