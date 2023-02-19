-- FPGA RemoCon Project: UART string sender/receiver w/ FIFO
-- 2021.07.26 - 2022.04.20 Naoki F., AIT
-----------------------------------------------------------------------
library IEEE;
use IEEE.std_logic_1164.ALL;

entity serial_fifo is
    generic ( WAIT_DIV : integer := 868 );
    port ( CLK, RST : in  std_logic;
           TXD      : out std_logic;
           RXD      : in  std_logic;
           DATA_IN  : in  std_logic_vector(7 downto 0); -- char from FPGA
           WE       : in  std_logic;  -- write enable
           FULL     : out std_logic;  -- whether fifo of DATA_IN is full
           DATA_OUT : out std_logic_vector(7 downto 0); -- char from PC
           RE       : in  std_logic;  -- read enable
           EMPTY    : out std_logic); -- whether fifo of DATA_OUT is empty
end serial_fifo;

architecture structure of serial_fifo is
    component serial_send is
        generic ( WAIT_DIV : integer := 868 ); -- 100 MHz / 115.2 kbps
        port ( CLK, RST : in  std_logic;
               DATA_IN  : in  std_logic_vector(7 downto 0);
               WE       : in  std_logic;
               DATA_OUT : out std_logic;
               BUSY     : out std_logic);
    end component;
    
    component serial_recv is
        generic ( WAIT_DIV : integer := 868 );
        port ( CLK, RST : in  std_logic;
               DATA_IN  : in  std_logic;
               DATA_OUT : out std_logic_vector(7 downto 0);
               VALID    : out std_logic);
    end component;

    component fifo is 
        generic ( WIDTH    : integer := 8;
                  SIZE     : integer := 2048;
                  LOG_SIZE : integer := 11 );
        port ( CLK, RST    : in  std_logic;
               DATA_W      : in  std_logic_vector(WIDTH-1 downto 0);
               DATA_R      : out std_logic_vector(WIDTH-1 downto 0);
               WE, RE      : in  std_logic;
               EMPTY, FULL : out std_logic);
    end component;
    
    signal fifo_i_data_w, fifo_i_data_r                    : std_logic_vector(7 downto 0);
    signal fifo_i_we, fifo_i_re, fifo_i_full, fifo_i_empty : std_logic;
    signal fifo_o_data_w, fifo_o_data_r                    : std_logic_vector(7 downto 0);
    signal fifo_o_we, fifo_o_re, fifo_o_full, fifo_o_empty : std_logic;
    signal send_data_in                                    : std_logic_vector(7 downto 0);
    signal send_data_out, send_we, send_busy               : std_logic;
    signal recv_data_out                                   : std_logic_vector(7 downto 0);
    signal recv_data_in, recv_valid                        : std_logic;

begin
    fifo_i: fifo
        port map (CLK, RST, fifo_i_data_w, fifo_i_data_r,
                  fifo_i_we, fifo_i_re, fifo_i_empty, fifo_i_full);
    fifo_o: fifo
        port map (CLK, RST, fifo_o_data_w, fifo_o_data_r,
                  fifo_o_we, fifo_o_re, fifo_o_empty, fifo_o_full);
    ser_send: serial_send
        generic map (WAIT_DIV)
        port map (CLK, RST, send_data_in, send_we, send_data_out, send_busy);
    ser_recv: serial_recv
        generic map (WAIT_DIV)
        port map (CLK, RST, recv_data_in, recv_data_out, recv_valid);

    -- Send FIFO input
    fifo_i_data_w <= DATA_IN;
    fifo_i_we     <= WE;
    fifo_i_re     <= not send_busy;
    -- Send FIFO output / Send Serial input
    FULL          <= fifo_i_full;
    send_data_in  <= fifo_i_data_r;
    send_we       <= not fifo_i_empty;
    -- Send Serial output
    TXD           <= send_data_out;

    -- Recv Serial input
    recv_data_in  <= RXD;
    -- Recv Serial output / Recv FIFO input
    fifo_o_data_w <= recv_data_out;
    fifo_o_we     <= recv_valid and not fifo_o_full;
    fifo_o_re     <= RE;
    -- Recv FIFO output
    DATA_OUT      <= fifo_o_data_r;
    EMPTY         <= fifo_o_empty;
end structure;