-- DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
-- Copyright (C) 2022 Naoki FUJIEDA. New BSD License is applied.
------------------------------------------------------------------------

library IEEE;
use IEEE.std_logic_1164.ALL;

-- This is a top module of the whole circuit implemented on an FPGA.
entity TOP is
    port ( SW                             : in  std_logic_vector(15 downto 0);
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

    signal INT_RST                                            : std_logic;
    signal INT_SW                                             : std_logic_vector(15 downto 0);
    signal INT_BTNC, INT_BTNL, INT_BTNR, INT_BTNU, INT_BTND   : std_logic;
    signal INT_LD                                             : std_logic_vector(15 downto 0);
    signal INT_AN                                             : std_logic_vector(7 downto 0);
    signal INT_CA, INT_CB, INT_CC, INT_CD                     : std_logic;
    signal INT_CE, INT_CF, INT_CG, INT_DP                     : std_logic;

begin
    process (CLK) begin
        if (rising_edge(CLK)) then
            INT_RST  <= not RST_X;
            INT_SW   <= SW;
            INT_BTNC <= BTNC;
            INT_BTNL <= BTNL;
            INT_BTNR <= BTNR;
            INT_BTNU <= BTNU;
            INT_BTND <= BTND;
            LD       <= INT_LD;
            AN       <= INT_AN;
            CA       <= INT_CA;
            CB       <= INT_CB;
            CC       <= INT_CC;
            CD       <= INT_CD;
            CE       <= INT_CE;
            CF       <= INT_CF;
            CG       <= INT_CG;
            DP       <= INT_DP;
        end if;
    end process;

    DR : DR_TOP port map (
        SW   => INT_SW,
        BTNC => INT_BTNC,
        BTNL => INT_BTNL,
        BTNR => INT_BTNR,
        BTNU => INT_BTNU,
        BTND => INT_BTND,
        CLK  => CLK,
        RST  => INT_RST,
        LD   => INT_LD,
        AN   => INT_AN,
        CA   => INT_CA,
        CB   => INT_CB,
        CC   => INT_CC,
        CD   => INT_CD,
        CE   => INT_CE,
        CF   => INT_CF,
        CG   => INT_CG,
        DP   => INT_DP);

end STRUCTURE;