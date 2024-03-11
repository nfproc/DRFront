-- DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
-- Copyright (C) 2022-2024 Naoki FUJIEDA. New BSD License is applied.
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
    signal light_pos, n_light_pos : std_logic_vector(2 downto 0);  -- position of LED/7-seg to be lit
    signal direction, n_direction : std_logic;                     -- '0': to left, '1': to right
    signal count, n_count         : std_logic_vector(22 downto 0); -- for 50-ms timer
    signal mcount, n_mcount       : std_logic_vector(3 downto 0);  -- for counter of moving interval
    signal mcount_en, move_en     : std_logic;

begin
    -- 50-ms timer
    n_count   <= count + '1'  when mcount_en = '0' else (others => '0');
    n_mcount  <= mcount       when mcount_en = '0' else
                 mcount + '1' when move_en = '0'   else "0000";
    mcount_en <= '1'          when count = 4999999 else '0';
    move_en   <= '1'          when mcount_en = '1' and mcount >= SW(3 downto 0) else '0';

    -- LED output (active high)
    process (light_pos, BTNU, BTND) begin
        for i in 0 to 3 loop -- lower half
            if light_pos = i then
                LD(i) <= not BTND;
            else
                LD(i) <= BTND;
            end if;
        end loop;
        for i in 0 to 3 loop -- upper half
            if light_pos = 4 + i then
                LD(7 - i) <= not BTNU;
            else
                LD(7 - i) <= BTNU;
            end if;
        end loop;
    end process;
    LD(15 downto 8) <= "00000000";

    -- next mode (decided by the buttons)
    n_direction <= '0'  when BTNR = '1' else
                   '1'  when BTNL = '1' else direction;
    
    -- next light position
    n_light_pos <= light_pos + '1' when direction = '0' else
                   light_pos - '1';

    -- update registers
    process (CLK) begin
        if (rising_edge(CLK)) then
            if (RST = '1') then
                light_pos <= "000";
                direction <= '0';
                count     <= (others => '0');
                mcount    <= "0000";
            else
                if (move_en = '1') then
                    light_pos <= n_light_pos;
                end if;
                direction <= n_direction;
                mcount    <= n_mcount;
                count     <= n_count;
            end if;
        end if;
    end process;
end RTL;
