-- FPGA RemoCon Project: UART character receiving module
-- 2021.07.26 - 2022.04.20 Naoki F., AIT
------------------------------------------------------------------------
library IEEE;
use IEEE.std_logic_1164.ALL;
use IEEE.std_logic_unsigned.ALL;

entity serial_recv is
    generic ( WAIT_DIV : integer := 868 ); -- 100 MHz / 115.2 kbps
    port ( CLK, RST : in  std_logic;
           DATA_IN  : in  std_logic;
           DATA_OUT : out std_logic_vector(7 downto 0);
           VALID    : out std_logic);
end serial_recv;

architecture RTL of serial_recv is
    type state_type is (STATE_IDLE, STATE_RECV);
    signal state, n_state         : state_type;
    signal data_reg, n_data_reg   : std_logic_vector(7 downto 0);
    signal wait_cnt, n_wait_cnt   : integer range 0 to (WAIT_DIV - 1);
    signal bit_cnt, n_bit_cnt     : std_logic_vector(3 downto 0);
    signal valid_reg, n_valid_reg : std_logic;
begin
    DATA_OUT <= data_reg;
    VALID    <= valid_reg;

    process (state, data_reg, wait_cnt, bit_cnt, valid_reg, DATA_IN) begin
        n_state     <= state;
        n_data_reg  <= data_reg;
        n_wait_cnt  <= wait_cnt;
        n_bit_cnt   <= bit_cnt;
        n_valid_reg <= '0';
        if (state = STATE_IDLE) then
            if (DATA_IN = '0') then -- start bit
                if (wait_cnt = WAIT_DIV - 1) then
                    n_state     <= STATE_RECV;
                    n_wait_cnt  <= 0;
                    n_bit_cnt   <= x"0";
                else
                    n_wait_cnt  <= wait_cnt + 1;
                end if;
            else
                n_wait_cnt  <= WAIT_DIV / 2;
            end if;
        elsif (state = STATE_RECV) then
            if (wait_cnt = WAIT_DIV - 1) then
                n_wait_cnt  <= 0;
                n_data_reg  <= DATA_IN & data_reg(7 downto 1);
                if (bit_cnt = x"8") then
                    n_state     <= STATE_IDLE;
                    n_bit_cnt   <= x"0";
                elsif (bit_cnt = x"7") then
                    n_valid_reg <= '1';
                    n_bit_cnt   <= bit_cnt + '1';
                else
                    n_bit_cnt   <= bit_cnt + '1';
                end if;
            else
                n_wait_cnt  <= wait_cnt + 1;
            end if;
        end if;
    end process;

    process (CLK) begin
        if (rising_edge(CLK)) then
            if (RST = '1') then
                state     <= STATE_IDLE;
                wait_cnt  <= WAIT_DIV / 2;
                bit_cnt   <= x"0";
                data_reg  <= x"00";
                valid_reg <= '0';
            else
                state     <= n_state;
                wait_cnt  <= n_wait_cnt;
                bit_cnt   <= n_bit_cnt;
                data_reg  <= n_data_reg;
                valid_reg <= n_valid_reg;
            end if;
        end if;
    end process;
end RTL;
