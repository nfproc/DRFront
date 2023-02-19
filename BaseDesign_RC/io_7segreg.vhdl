-- FPGA RemoCon Project: register for 7-seg LED output
-- 2022.04.21 Naoki F., AIT
------------------------------------------------------------------------
library IEEE;
use IEEE.std_logic_1164.ALL;

entity io_7segreg is
    generic ( SAMPLE_INT : integer := 1000000; -- sampling interval
              STABLE_INT : integer := 16);     -- cycles to see if input is stable
    port ( CLK, RST : in  std_logic;
           AN       : in  std_logic_vector( 3 downto 0); -- active low
           SEG      : in  std_logic_vector( 7 downto 0); -- active low
           LED_OUT  : out std_logic_vector(31 downto 0));
end io_7segreg;

architecture RTL of io_7segreg is
    signal cnt_sample, n_cnt_sample   : integer range 0 to (SAMPLE_INT - 1);
    signal cnt_stable, n_cnt_stable   : integer range 0 to (STABLE_INT - 1);
    signal an_reg                     : std_logic_vector( 3 downto 0);
    signal seg_reg                    : std_logic_vector( 7 downto 0);
    signal accum, n_accum             : std_logic_vector(31 downto 0);
    signal led_out_reg, n_led_out_reg : std_logic_vector(31 downto 0);
begin
    LED_OUT <= led_out_reg;

    process (cnt_sample, cnt_stable, accum, led_out_reg, an_reg, seg_reg, AN, SEG) begin
        n_accum       <= accum;
        n_led_out_reg <= led_out_reg;

        -- next counter value
        if (cnt_sample = SAMPLE_INT - 1) then
            n_cnt_sample  <= 0;
        else
            n_cnt_sample  <= cnt_sample + 1;
        end if;
        if (AN /= an_reg or SEG /= seg_reg or cnt_stable = STABLE_INT - 1) then
            n_cnt_stable  <= 0;
        else
            n_cnt_stable  <= cnt_stable + 1;
        end if;

        if (cnt_sample = SAMPLE_INT - 1) then
            -- update output register and clear accumulation register
            n_led_out_reg <= accum;
            n_accum       <= (others => '0');
        elsif (cnt_stable = STABLE_INT - 1) then
            -- check and accumulate LEDs lit
            for i in 0 to 3 loop
                if (an_reg(i) = '0') then
                    n_accum((i * 8 + 7) downto (i * 8))
                        <= accum((i * 8 + 7) downto (i * 8)) or not seg_reg;
                end if;
            end loop;
        end if;
    end process;

    process (CLK) begin
        if (rising_edge(CLK)) then
            if (RST = '1') then
                cnt_sample  <= 0;
                cnt_stable  <= 0;
                an_reg      <= (others => '0');
                seg_reg     <= (others => '0');
                accum       <= (others => '0');
                led_out_reg <= (others => '0');
            else
                cnt_sample  <= n_cnt_sample;
                cnt_stable  <= n_cnt_stable;
                an_reg      <= AN;
                seg_reg     <= SEG;
                accum       <= n_accum;
                led_out_reg <= n_led_out_reg;
            end if;
        end if;
    end process;
end RTL;

