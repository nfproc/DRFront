-- FPGA RemoCon Project: Top module for DRFront (Arty)
-- 2024.03.08 Naoki F., AIT
------------------------------------------------------------------------
library IEEE;
use IEEE.std_logic_1164.ALL;
use IEEE.std_logic_unsigned.ALL;

entity TOP is
    generic ( WAIT_DIV : integer := 868 );
    port ( SW                     : in  std_logic_vector(3 downto 0);
           TXD                    : out std_logic;
           RXD                    : in  std_logic;
           BTNL, BTNR, BTNU, BTND : in  std_logic;
           CLK, RST_X             : in  std_logic;
           LD                     : out std_logic_vector(7 downto 0));
end TOP;

architecture STRUCTURE of TOP is
    component DR_TOP is
        port ( SW                             : in  std_logic_vector(15 downto 0);
               BTNC, BTNL, BTNR, BTNU, BTND   : in  std_logic;
               CLK, RST                       : in  std_logic;
               LD                             : out std_logic_vector(15 downto 0);
               AN                             : out std_logic_vector(7 downto 0);
               CA, CB, CC, CD, CE, CF, CG, DP : out std_logic);
    end component;

    component uart_switch is
        generic ( WAIT_DIV : integer := 868 );
        port ( CLK, RST : in  std_logic;
               TXD      : out std_logic;
               RXD      : in  std_logic;
               BOARD_SW  : in  std_logic_vector(12 downto 0);
               BOARD_LED : out std_logic_vector( 7 downto 0);
               BOARD_AN  : out std_logic_vector( 3 downto 0);
               BOARD_SEG : out std_logic_vector( 7 downto 0);
               USER_SW   : out std_logic_vector(12 downto 0);
               USER_LED  : in  std_logic_vector( 7 downto 0);
               USER_AN   : in  std_logic_vector( 3 downto 0);
               USER_SEG  : in  std_logic_vector( 7 downto 0));
    end component;

    component ila_0 is
        port ( clk     : in std_logic;
               probe0  : in std_logic_vector(7 downto 0);
               probe1  : in std_logic_vector(7 downto 0);
               probe2  : in std_logic_vector(7 downto 0);
               probe3  : in std_logic;
               probe4  : in std_logic;
               probe5  : in std_logic;
               probe6  : in std_logic;
               probe7  : in std_logic;
               probe8  : in std_logic;
               probe9  : in std_logic;
               probe10  : in std_logic;
               probe11  : in std_logic;
               probe12 : in std_logic;
               probe13 : in std_logic;
               probe14 : in std_logic;
               probe15 : in std_logic;
               probe16 : in std_logic;
               probe17 : in std_logic);
    end component;

    signal int_rst, n_int_rst                          : std_logic;
    signal int_sw                                      : std_logic_vector( 3 downto 0);
    signal int_btnl, int_btnr, int_btnu, int_btnd      : std_logic;
    signal int_ld                                      : std_logic_vector( 7 downto 0);
    signal count_rst, n_count_rst                      : std_logic_vector( 5 downto 0);
    
    signal dr_sw                                       : std_logic_vector(15 downto 0);
    signal dr_btnc, dr_btnl, dr_btnr, dr_btnu, dr_btnd : std_logic;
    signal dr_ld                                       : std_logic_vector(15 downto 0);
    signal dr_an                                       : std_logic_vector( 7 downto 0);
    signal dr_ca, dr_cb, dr_cc, dr_cd                  : std_logic;
    signal dr_ce, dr_cf, dr_cg, dr_dp                  : std_logic;

    signal u_board_sw, u_user_sw                       : std_logic_vector(12 downto 0);
    signal u_board_led, u_user_led                     : std_logic_vector( 7 downto 0);
    signal u_board_an, u_user_an                       : std_logic_vector( 3 downto 0);
    signal u_board_seg, u_user_seg                     : std_logic_vector( 7 downto 0);
    
    signal LD_o                                        : std_logic_vector( 7 downto 0);

    signal capture_1kHz, capture_1MHz                  : std_logic;
    signal count_1kHz, n_count_1kHz                    : std_logic_vector(16 downto 0);
    signal count_1MHz, n_count_1MHz                    : std_logic_vector( 6 downto 0);

begin
    -- external reset (asynchronous)
    n_int_rst   <= '0'      when count_rst = "111111" else '1';
    n_count_rst <= "111111" when count_rst = "111111" else count_rst + '1';

    process (CLK, RST_X) begin
        if (RST_X = '0') then
            int_rst   <= '1';
            count_rst <= "000000";
        elsif (rising_edge(CLK)) then
            int_rst   <= n_int_rst;
            count_rst <= n_count_rst;
        end if;
    end process;

    -- internal register for input/output ports
    int_ld <= u_board_led;

    process (CLK) begin
        if (rising_edge(CLK)) then
            int_sw    <= SW;
            int_btnl  <= BTNL;
            int_btnr  <= BTNR;
            int_btnu  <= BTNU;
            int_btnd  <= BTND;
            LD_o      <= int_ld;
        end if;
    end process;

    -- instantiation of DR_TOP
    dr_sw   <= x"00" & u_user_sw(7 downto 0);
    dr_btnc <= u_user_sw(9);
    dr_btnl <= u_user_sw(10);
    dr_btnr <= u_user_sw(8);
    dr_btnu <= u_user_sw(12);
    dr_btnd <= u_user_sw(11);

    DR : DR_TOP port map (
        SW   => dr_sw,
        BTNC => dr_btnc,
        BTNL => dr_btnl,
        BTNR => dr_btnr,
        BTNU => dr_btnu,
        BTND => dr_btnd,
        CLK  => CLK,
        RST  => int_rst,
        LD   => dr_ld,
        AN   => dr_an,
        CA   => dr_ca,
        CB   => dr_cb,
        CC   => dr_cc,
        CD   => dr_cd,
        CE   => dr_ce,
        CF   => dr_cf,
        CG   => dr_cg,
        DP   => dr_dp);

    -- instantiation of uart_switch
    u_board_sw <= int_btnu & int_btnd & int_btnl & '0' & int_btnr & x"0" & int_sw(3 downto 0);
    u_user_led <= dr_ld(7 downto 0);
    u_user_an  <= dr_an(3 downto 0);
    u_user_seg <= dr_dp & dr_cg & dr_cf & dr_ce & dr_cd & dr_cc & dr_cb & dr_ca;

    UART : uart_switch generic map (WAIT_DIV => WAIT_DIV) port map (
        CLK       => CLK,
        RST       => int_rst,
        TXD       => TXD,
        RXD       => RXD,
        BOARD_SW  => u_board_sw,
        BOARD_LED => u_board_led,
        BOARD_AN  => u_board_an,
        BOARD_SEG => u_board_seg,
        USER_SW   => u_user_sw,
        USER_LED  => u_user_led,
        USER_AN   => u_user_an,
        USER_SEG  => u_user_seg);

    -- board output (~33% PWM for RGB LEDs)
    LD(3 downto 0) <= LD_o(3 downto 0) when count_1kHz < 32768 else x"0";
    LD(7 downto 4) <= LD_o(7 downto 4);

    -- instantiation of integrated logic analyzer (ILA)
    ILA : ila_0 port map (
        clk     => CLK,
        probe0  => dr_ld(7 downto 0),
        probe1  => dr_sw(7 downto 0),
        probe2  => dr_an,
        probe3  => dr_ca,
        probe4  => dr_cb,
        probe5  => dr_cc,
        probe6  => dr_cd,
        probe7  => dr_ce,
        probe8  => dr_cf,
        probe9  => dr_cg,
        probe10 => dr_dp,
        probe11 => dr_btnr,
        probe12 => dr_btnc,
        probe13 => dr_btnl,
        probe14 => dr_btnd,
        probe15 => dr_btnu,
        probe16 => capture_1kHz,
        probe17 => capture_1MHz);

    -- capture signals
    capture_1kHz <= '0'              when count_1kHz /= 0 else '1';
    n_count_1kHz <= count_1kHz - '1' when count_1kHz /= 0 else "11000011010011111";
    capture_1MHz <= '0'              when count_1MHz /= 0 else '1';
    n_count_1MHz <= count_1MHz - '1' when count_1MHz /= 0 else "1100011";

    process (CLK) begin
        if (rising_edge(CLK)) then
            if (int_rst = '1') then
                count_1kHz <= "11000011010011111"; -- 99,999
                count_1MHz <= "1100011";           -- 99
            else
                count_1kHz <= n_count_1kHz;
                count_1MHz <= n_count_1MHz;
            end if;
        end if;
    end process;

end STRUCTURE;