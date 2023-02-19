-- FPGA RemoCon Project: Top module for DRFront
-- 2022.04.21 Naoki F., AIT
------------------------------------------------------------------------
library IEEE;
use IEEE.std_logic_1164.ALL;

entity TOP is
    generic ( WAIT_DIV : integer := 868 );
    port ( SW                             : in  std_logic_vector(15 downto 0);
           TXD                            : out std_logic;
           RXD                            : in  std_logic;
           BTNC, BTNL, BTNR, BTNU, BTND   : in  std_logic;
           CLK, RST_X                     : in  std_logic;
           LD                             : out std_logic_vector(15 downto 0);
           AN                             : out std_logic_vector(7 downto 0);
           CA, CB, CC, CD, CE, CF, CG, DP : out std_logic);
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
               BOARD_SW  : in  std_logic_vector(10 downto 0);
               BOARD_LED : out std_logic_vector( 7 downto 0);
               BOARD_AN  : out std_logic_vector( 3 downto 0);
               BOARD_SEG : out std_logic_vector( 7 downto 0);
               USER_SW   : out std_logic_vector(10 downto 0);
               USER_LED  : in  std_logic_vector( 7 downto 0);
               USER_AN   : in  std_logic_vector( 3 downto 0);
               USER_SEG  : in  std_logic_vector( 7 downto 0));
    end component;

    signal int_rst                                          : std_logic;
    signal int_sw                                           : std_logic_vector(15 downto 0);
    signal int_btnc, int_btnl, int_btnr, int_btnu, int_btnd : std_logic;
    signal int_ld                                           : std_logic_vector(15 downto 0);
    signal int_an                                           : std_logic_vector( 7 downto 0);
    signal int_ca, int_cb, int_cc, int_cd                   : std_logic;
    signal int_ce, int_cf, int_cg, int_dp                   : std_logic;
    
    signal dr_sw                                            : std_logic_vector(15 downto 0);
    signal dr_btnc, dr_btnl, dr_btnr, dr_btnu, dr_btnd      : std_logic;
    signal dr_ld                                            : std_logic_vector(15 downto 0);
    signal dr_an                                            : std_logic_vector(7 downto 0);
    signal dr_ca, dr_cb, dr_cc, dr_cd                       : std_logic;
    signal dr_ce, dr_cf, dr_cg, dr_dp                       : std_logic;

    signal u_board_sw, u_user_sw                            : std_logic_vector(10 downto 0);
    signal u_board_led, u_user_led                          : std_logic_vector( 7 downto 0);
    signal u_board_an, u_user_an                            : std_logic_vector( 3 downto 0);
    signal u_board_seg, u_user_seg                          : std_logic_vector( 7 downto 0);

begin
    -- internal register for input/output ports
    int_ld <= dr_ld(15 downto 8) & u_board_led;
    int_an <= dr_an( 7 downto 4) & u_board_an;
    int_ca <= u_board_seg(0);
    int_cb <= u_board_seg(1);
    int_cc <= u_board_seg(2);
    int_cd <= u_board_seg(3);
    int_ce <= u_board_seg(4);
    int_cf <= u_board_seg(5);
    int_cg <= u_board_seg(6);
    int_dp <= u_board_seg(7);

    process (CLK) begin
        if (rising_edge(CLK)) then
            int_rst  <= not RST_X;
            int_sw   <= SW;
            int_btnc <= BTNC;
            int_btnl <= BTNL;
            int_btnr <= BTNR;
            int_btnu <= BTNU;
            int_btnd <= BTND;
            LD       <= int_ld;
            AN       <= int_an;
            CA       <= int_ca;
            CB       <= int_cb;
            CC       <= int_cc;
            CD       <= int_cd;
            CE       <= int_ce;
            CF       <= int_cf;
            CG       <= int_cg;
            DP       <= int_dp;
        end if;
    end process;

    -- instantiation of DR_TOP
    dr_sw   <= int_sw(15 downto 8) & u_user_sw(7 downto 0);
    dr_btnc <= u_user_sw(9);
    dr_btnl <= u_user_sw(10);
    dr_btnr <= u_user_sw(8);
    dr_btnu <= int_btnu;
    dr_btnd <= int_btnd;

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
    u_board_sw <= int_btnl & int_btnc & int_btnr & int_sw(7 downto 0);
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

end STRUCTURE;