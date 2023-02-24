-- FPGA RemoCon Project: register for LED array output
-- 2023.02.24 Naoki F., AIT
------------------------------------------------------------------------
library IEEE;
use IEEE.std_logic_1164.ALL;

entity io_ledreg is
    generic ( SAMPLE_INT : integer := 200000; -- sampling interval
              STABLE_INT : integer := 16);    -- cycles to see if input is stable
    port ( CLK, RST : in  std_logic;
           LED_IN   : in  std_logic_vector( 7 downto 0);
           LED_OUT  : out std_logic_vector( 7 downto 0));
end io_ledreg;

architecture RTL of io_ledreg is
    type stable_t is array (7 downto 0) of integer range 0 to (STABLE_INT - 1);
    type value_t  is array (7 downto 0) of integer range 0 to (SAMPLE_INT * 2 - 1);

    signal cnt_sample,    n_cnt_sample    : integer range 0 to (SAMPLE_INT - 1);
    signal cnt_threshold, n_cnt_threshold : integer range 0 to 3;
    signal cnt_stable,    n_cnt_stable    : stable_t;
    signal cnt_value,     n_cnt_value     : value_t;
    signal led_out_reg,   n_led_out_reg   : std_logic_vector(7 downto 0);
begin
    LED_OUT <= led_out_reg;

    process (cnt_sample, cnt_threshold, cnt_stable, cnt_value, led_out_reg, LED_IN) begin
        n_led_out_reg   <= led_out_reg;

        -- sampling counter
        if cnt_sample = SAMPLE_INT - 1 then
            n_cnt_sample  <= 0;
            if cnt_threshold = 3 then
                n_cnt_threshold <= 0;
            else
                n_cnt_threshold <= cnt_threshold + 1;
            end if;
        else
            n_cnt_sample  <= cnt_sample + 1;
            n_cnt_threshold <= cnt_threshold;
        end if;

        for i in 0 to 7 loop
            -- counter to see if output of '1' is continued for certain cycles
            if LED_IN(i) = '1' and cnt_stable(i) = STABLE_INT - 1 then
                n_cnt_stable(i) <= cnt_stable(i);
            elsif LED_IN(i) = '1' then
                n_cnt_stable(i) <= cnt_stable(i) + 1;
            else
                n_cnt_stable(i) <= 0; 
            end if;

            -- output LED value
            if cnt_sample = SAMPLE_INT - 1 then
                n_cnt_value(i) <= 0;
                if cnt_value(i) >= SAMPLE_INT / 4 * (cnt_threshold * 2 + 1) then
                    n_led_out_reg(i) <= '1';
                else
                    n_led_out_reg(i) <= '0';
                end if;
            elsif LED_IN(i) = '1' and cnt_stable(i) = STABLE_INT - 1 then
                n_cnt_value(i)  <= cnt_value(i) + 2;
            elsif LED_IN(i) = '1' then
                n_cnt_value(i)  <= cnt_value(i) + 1;
            else
                n_cnt_value(i)  <= cnt_value(i);
            end if;
        end loop;
    end process;

    process (CLK) begin
        if (rising_edge(CLK)) then
            if (RST = '1') then
                cnt_sample    <= 0;
                cnt_threshold <= 0;
                for i in 0 to 7 loop
                    cnt_stable(i) <= 0;
                    cnt_value(i)  <= 0;
                end loop;
                led_out_reg <= (others => '0');
            else
                cnt_sample    <= n_cnt_sample;
                cnt_threshold <= n_cnt_threshold;
                for i in 0 to 7 loop
                    cnt_stable(i) <= n_cnt_stable(i);
                    cnt_value(i)  <= n_cnt_value(i);
                end loop;
                led_out_reg <= n_led_out_reg;
            end if;
        end if;
    end process;
end RTL;

