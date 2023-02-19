-- FIFO module (Developed independently from FPGA RemoCon Project)
-- 2020.08.25 Naoki F., AIT. New BSD license is applied.
------------------------------------------------------------------------
library IEEE;
use IEEE.std_logic_1164.ALL;
use IEEE.std_logic_unsigned.ALL;

entity fifo is
    generic ( WIDTH    : integer := 8;
              SIZE     : integer := 2048;
              LOG_SIZE : integer := 11 );
    port ( CLK, RST    : in  std_logic;
           DATA_W      : in  std_logic_vector(WIDTH-1 downto 0);
           DATA_R      : out std_logic_vector(WIDTH-1 downto 0);
           WE, RE      : in  std_logic;
           EMPTY, FULL : out std_logic);
end fifo;

architecture RTL of fifo is
    type ram_type is array(0 to SIZE-1) of std_logic_vector(WIDTH-1 downto 0);
    signal fifo_ram     : ram_type;
    signal ram_out      : std_logic_vector(WIDTH-1 downto 0);
    signal d_data_w     : std_logic_vector(WIDTH-1 downto 0);
    signal ram_select   : std_logic;
    signal write_valid  : std_logic;
    signal read_valid   : std_logic;
    signal head, n_head : std_logic_vector(LOG_SIZE-1 downto 0);
    signal tail, n_tail : std_logic_vector(LOG_SIZE-1 downto 0);
    signal c_empty, n_empty, near_empty, n_near_empty : std_logic;
    signal c_full , n_full , near_full , n_near_full  : std_logic;
begin
    EMPTY <= c_empty;
    FULL  <= c_full;

    -- RAM: dealing with read from and write to the same address (n_head == tail)
    DATA_R <= ram_out when ram_select = '1' else d_data_w;

    process (CLK) begin
        if (rising_edge(CLK)) then
            if (write_valid = '1') then
                if (n_head /= tail) then
                    ram_select <= '1';
                else
                    ram_select <= '0';
                end if;
                d_data_w <= DATA_W;
            else
                ram_select <= '1';
            end if;
        end if;
    end process;
    
    process (CLK) begin -- これが RAM 本体
        if (rising_edge(CLK)) then
            ram_out <= fifo_ram(conv_integer(n_head));
            if (write_valid = '1') then
                fifo_ram(conv_integer(tail)) <= data_w;
            end if;
        end if;
    end process;

    -- read/write control (combinatorial)
    read_valid   <= RE and not c_empty;
    write_valid  <= WE and not c_full;
    n_head       <= head + '1' when read_valid  = '1' else head;
    n_tail       <= tail + '1' when write_valid = '1' else tail;
    n_empty      <= not write_valid and (c_empty or (read_valid  and near_empty));
    n_full       <= not read_valid  and (c_full  or (write_valid and near_full));
    n_near_empty <= '1' when n_head + '1' = n_tail else '0';
    n_near_full  <= '1' when n_head = n_tail + '1' else '0';

    -- update of registers
    process (CLK) begin
        if (rising_edge(CLK)) then
            if (RST = '1') then
                head       <= (others => '0');
                tail       <= (others => '0');
                c_empty    <= '1';
                c_full     <= '0';
                near_empty <= '0';
                near_full  <= '0';
            else
                head       <= n_head;
                tail       <= n_tail;
                c_empty    <= n_empty;
                c_full     <= n_full;
                near_empty <= n_near_empty;
                near_full  <= n_near_full;
            end if;
        end if;
    end process;
end RTL;
