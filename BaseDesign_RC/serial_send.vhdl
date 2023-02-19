-- UART character sender (developed independently from FPGA RemoCon)
-- 2020.03.16 - 2020.12.17 Naoki F., AIT. New BSD license is applied.
------------------------------------------------------------------------
library IEEE;
use IEEE.std_logic_1164.ALL;
use IEEE.std_logic_unsigned.ALL;

entity serial_send is
    generic ( WAIT_DIV : integer := 868 ); -- 100 MHz / 115.2 kbps
    port ( CLK, RST : in  std_logic;
           DATA_IN  : in  std_logic_vector(7 downto 0);
           WE       : in  std_logic;
           DATA_OUT : out std_logic;
           BUSY     : out std_logic);
end serial_send;

architecture RTL of serial_send is
    type state_type is (STATE_IDLE, STATE_SEND);
    signal state, n_state       : state_type;
    signal data_reg, n_data_reg : std_logic_vector(9 downto 0);
    signal wait_cnt, n_wait_cnt : integer range 0 to (WAIT_DIV - 1);
    signal bit_cnt, n_bit_cnt   : std_logic_vector(3 downto 0);
begin
    DATA_OUT <= data_reg(0);

    process (state, data_reg, wait_cnt, bit_cnt, DATA_IN, WE) begin
        BUSY       <= '0';
        n_state    <= state;
        n_wait_cnt <= wait_cnt;
        n_bit_cnt  <= bit_cnt;
        n_data_reg <= data_reg;
        if (state = STATE_IDLE) then
            if (WE = '1') then
                n_state    <= STATE_SEND;
                n_data_reg <= '1' & DATA_IN & '0';
            end if;
        elsif (state = STATE_SEND) then
            BUSY       <= '1';
            if (wait_cnt = WAIT_DIV - 1) then
                if (bit_cnt = x"9") then
                    n_state    <= STATE_IDLE;
                    n_wait_cnt <= 0;
                    n_bit_cnt  <= x"0";
                else
                    n_data_reg <= '1' & data_reg(9 downto 1);
                    n_wait_cnt <= 0;
                    n_bit_cnt  <= bit_cnt + '1';
                end if;
            else
                n_wait_cnt <= wait_cnt + 1;
            end if;
        end if;
    end process;

    process (CLK) begin
        if (rising_edge(CLK)) then
            if (RST = '1') then
                state    <= STATE_IDLE;
                wait_cnt <= 0;
                bit_cnt  <= x"0";
                data_reg <= (others => '1');
            else
                state    <= n_state;
                wait_cnt <= n_wait_cnt;
                bit_cnt  <= n_bit_cnt;
                data_reg <= n_data_reg;
            end if;
        end if;
    end process;
end RTL;
