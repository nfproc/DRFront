-- FPGA RemoCon Project: UART Switch for handshake with PC
-- 2021.11.04 - 2023.07.13 Naoki F., AIT
------------------------------------------------------------------------
library IEEE;
use IEEE.std_logic_1164.ALL;
use IEEE.std_logic_unsigned.ALL;

entity uart_switch is
    generic ( WAIT_DIV : integer := 868 );
    port ( CLK, RST  : in  std_logic;
           TXD       : out std_logic;
           RXD       : in  std_logic;
           BOARD_SW  : in  std_logic_vector(12 downto 0);
           BOARD_LED : out std_logic_vector( 7 downto 0);
           BOARD_AN  : out std_logic_vector( 3 downto 0);
           BOARD_SEG : out std_logic_vector( 7 downto 0);
           USER_SW   : out std_logic_vector(12 downto 0);
           USER_LED  : in  std_logic_vector( 7 downto 0);
           USER_AN   : in  std_logic_vector( 3 downto 0);
           USER_SEG  : in  std_logic_vector( 7 downto 0));
end uart_switch;

architecture RTL of uart_switch is
    constant HELLO_SEND1 : std_logic_vector(7 downto 0) := x"56"; -- 'V'
    constant HELLO_SEND2 : std_logic_vector(7 downto 0) := x"58"; -- 'X'
    constant RESET_SEND2 : std_logic_vector(7 downto 0) := x"5a"; -- 'Z'
    constant FPGA_RECV1  : std_logic_vector(7 downto 0) := x"76"; -- 'v'
    constant FPGA_RECV2  : std_logic_vector(7 downto 0) := x"78"; -- 'x'

    -- component declarations
    component serial_fifo is
        generic ( WAIT_DIV : integer := 868 );
        port ( CLK, RST : in  std_logic;
               TXD      : out std_logic;
               RXD      : in  std_logic;
               DATA_IN  : in  std_logic_vector(7 downto 0);
               WE       : in  std_logic;
               FULL     : out std_logic;
               DATA_OUT : out std_logic_vector(7 downto 0);
               RE       : in  std_logic; 
               EMPTY    : out std_logic);
    end component;
    
    component io_translator is
        generic ( SAMPLE_INT : integer := 1000000;
                  STABLE_INT : integer := 16);    
        port ( CLK, RST  : in  std_logic;
               BOARD_SW  : in  std_logic_vector(12 downto 0);
               BOARD_LED : out std_logic_vector( 7 downto 0);
               BOARD_AN  : out std_logic_vector( 3 downto 0);
               BOARD_SEG : out std_logic_vector( 7 downto 0);
               USER_SW   : out std_logic_vector(12 downto 0);
               USER_LED  : in  std_logic_vector( 7 downto 0);
               USER_AN   : in  std_logic_vector( 3 downto 0);
               USER_SEG  : in  std_logic_vector( 7 downto 0);
               RST_REG   : in  std_logic;
               DATA_OUT  : out std_logic_vector( 7 downto 0);
               WE        : out std_logic;                    
               FULL      : in  std_logic;                    
               DATA_IN   : in  std_logic_vector( 7 downto 0);
               RE        : out std_logic;                    
               EMPTY     : in  std_logic);                   
    end component;

    -- signals connected to other circuits
    signal fifo_data_in, fifo_data_out                 : std_logic_vector(7 downto 0);
    signal fifo_we, fifo_full, fifo_re, fifo_empty     : std_logic;
    signal trans_rst_reg                               : std_logic;
    signal trans_data_in, trans_data_out               : std_logic_vector(7 downto 0);
    signal trans_we, trans_full, trans_re, trans_empty : std_logic;

    -- control signals
    signal cont_data_in                   : std_logic_vector(7 downto 0);
    signal cont_data_out, n_cont_data_out : std_logic_vector(7 downto 0);
    signal sel_data_in, sel_data_out      : std_logic; -- select trans_* if '0', cont_* if '1'
    signal cont_we, cont_re               : std_logic;
    signal sel_we, sel_re                 : std_logic;
    signal cont_stop                      : std_logic; -- set user's full/empty to '1' if '1'

    -- states of the circuit
    type state_type is (
        STATE_INIT,   -- before request is sent from PC
        STATE_HELLO1, -- after 1st char of request from PC (if request is not received)
        STATE_SEND1,  -- sending 1st char of response to PC
        STATE_SEND2,  -- sending 2nd char of response to PC
        STATE_READY,  -- after receiving request (connect user circuit and UART)
        STATE_HELLO2, -- after 1st char of request from PC (if request has been received)
        STATE_TRANS); -- sending char received at HELLO2 state
    signal state, n_state : state_type;

begin
    -- connection between UART and user circuit
    fifo_data_in  <= cont_data_in  when sel_data_in  = '1' else trans_data_out;
    fifo_we       <= cont_we       when sel_we       = '1' else trans_we;
    trans_full    <= '1'           when cont_stop    = '1' else fifo_full;
    trans_data_in <= cont_data_out when sel_data_out = '1' else fifo_data_out;
    fifo_re       <= cont_re       when sel_re       = '1' else trans_re;
    trans_empty   <= '1'           when cont_stop    = '1' else fifo_empty;

    -- next state of the switch (combinatorial)
    process (state, cont_data_out, fifo_data_out, fifo_empty, fifo_full, trans_re) begin
        n_state         <= state;
        cont_data_in    <= x"00";
        sel_data_in     <= '0';
        cont_we         <= '0';
        sel_we          <= '0';
        n_cont_data_out <= cont_data_out;
        sel_data_out    <= '0';
        cont_re         <= '0';
        sel_re          <= '0';
        cont_stop       <= '0';
        trans_rst_reg   <= '0';
        if (state = STATE_INIT) then
            -- INIT: wait until 1st request char is received
            cont_we         <= '0';
            sel_we          <= '1';
            cont_re         <= '1';
            sel_re          <= '1';
            cont_stop       <= '1';
            if (fifo_empty = '0' and fifo_data_out = HELLO_SEND1) then
                n_state         <= STATE_HELLO1;
            end if;
        elsif (state = STATE_HELLO1) then
            -- HELLO1 (previous state = INIT): wait until next char is received
            -- send response and reset translator if 2nd request char comes
            -- go back to previous state (INIT)   otherwise
            cont_we         <= '0';
            sel_we          <= '1';
            cont_re         <= '1';
            sel_re          <= '1';
            cont_stop       <= '1';
            if (fifo_empty = '0') then
                if (fifo_data_out = HELLO_SEND2) then
                    n_state         <= STATE_SEND1;
                    trans_rst_reg   <= '1';
                else
                    n_state         <= STATE_INIT;
                end if;
            end if;
        elsif (state = STATE_HELLO1 or state = STATE_HELLO2) then
            -- HELLO2 (previous state = READY): wait until next char is received
            -- send response                  if 2nd request char comes
            -- reset translator               if reset char comes
            -- send received char and go back otherwise
            cont_we         <= '0';
            sel_we          <= '1';
            cont_re         <= '1';
            sel_re          <= '1';
            cont_stop       <= '1';
            if (fifo_empty = '0') then
                if (fifo_data_out = HELLO_SEND2) then
                    n_state         <= STATE_SEND1;
                elsif (fifo_data_out = RESET_SEND2) then
                    n_state         <= STATE_READY;
                    trans_rst_reg   <= '1';
                else
                    n_state         <= STATE_TRANS;
                    n_cont_data_out <= fifo_data_out; -- received char has to be sent
                end if;
            end if;
        elsif (state = STATE_SEND1 or state = STATE_SEND2) then
            -- SEND1/SEND2: wait until a response char is sent
            if (state = STATE_SEND1) then
                cont_data_in    <= FPGA_RECV1;
            else
                cont_data_in    <= FPGA_RECV2;
            end if;
            sel_data_in     <= '1';
            cont_we         <= '1';
            sel_we          <= '1';
            cont_re         <= '0';
            sel_re          <= '1';
            cont_stop       <= '1';
            if (fifo_full = '0') then
                if (state = STATE_SEND1) then
                    n_state         <= STATE_SEND2;
                else
                    n_state         <= STATE_READY;
                end if;
            end if;
        elsif (state = STATE_READY) then
            -- READY: check if 1st char of request is received
            if (trans_re = '1' and fifo_empty = '0' and fifo_data_out = HELLO_SEND1) then
                n_state         <= STATE_HELLO2;
            end if;
        else -- state = STATE_TRANS
            -- TRANS: send the char received at HELLO2 state
            sel_data_out    <= '1';
            cont_re         <= '0';
            sel_re          <= '1';
            if (trans_re = '1' and fifo_empty = '0') then            
                n_state         <= STATE_READY;
            end if;
        end if;
    end process;
    
    -- state update of the switch (sequential)
    process (CLK) begin
        if (rising_edge(CLK)) then
            if (RST = '1') then
                state         <= STATE_INIT;
                cont_data_out <= x"00";
            else
                state         <= n_state;
                cont_data_out <= n_cont_data_out;
            end if;
        end if;
    end process;

    -- instantiation of components
    ser: serial_fifo
        generic map (WAIT_DIV)
        port map (CLK, RST, TXD, RXD, fifo_data_in, fifo_we, fifo_full,
                  fifo_data_out, fifo_re, fifo_empty);
    io: io_translator
        port map (CLK, RST, BOARD_SW, BOARD_LED, BOARD_AN, BOARD_SEG,
                  USER_SW, USER_LED, USER_AN, USER_SEG, trans_rst_reg,
                  trans_data_out, trans_we, trans_full,
                  trans_data_in, trans_re, trans_empty);
end RTL;