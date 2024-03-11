-- FPGA RemoCon Project: MMCM Instantiation for 100-MHz Clock
-- 2024.03.11 Naoki F., AIT
------------------------------------------------------------------------
library IEEE;
use IEEE.std_logic_1164.ALL;
use IEEE.std_logic_unsigned.ALL;

Library UNISIM;
use UNISIM.vcomponents.all;

entity MMCM_CMOD is
    port ( CLK_IN, RST     : in  std_logic;
           CLK_OUT, LOCKED : out std_logic);
end MMCM_CMOD;

architecture STRUCTURE of MMCM_CMOD is
    signal clk_out_int, clk_fb : std_logic;

begin
    outbuf : BUFG port map (
        I => clk_out_int,
        O => CLK_OUT);
    
    mmcm_inst : MMCME2_BASE generic map (
        BANDWIDTH          => "OPTIMIZED",
        CLKFBOUT_MULT_F    => 50.0,   -- CLK_VCO: 12 x 50 = 600 MHz
        CLKFBOUT_PHASE     => 0.0,
        CLKIN1_PERIOD      => 83.333, -- CLK_IN: 12 MHz (83.333 ns)
        CLKOUT1_DIVIDE     => 1,
        CLKOUT2_DIVIDE     => 1,
        CLKOUT3_DIVIDE     => 1,
        CLKOUT4_DIVIDE     => 1,
        CLKOUT5_DIVIDE     => 1,
        CLKOUT6_DIVIDE     => 1,
        CLKOUT0_DIVIDE_F   => 6.0,    -- CLK_OUT: 600 / 6 = 100 MHz
        CLKOUT0_DUTY_CYCLE => 0.5,
        CLKOUT1_DUTY_CYCLE => 0.5,
        CLKOUT2_DUTY_CYCLE => 0.5,
        CLKOUT3_DUTY_CYCLE => 0.5,
        CLKOUT4_DUTY_CYCLE => 0.5,
        CLKOUT5_DUTY_CYCLE => 0.5,
        CLKOUT6_DUTY_CYCLE => 0.5,
        CLKOUT0_PHASE      => 0.0,
        CLKOUT1_PHASE      => 0.0,
        CLKOUT2_PHASE      => 0.0,
        CLKOUT3_PHASE      => 0.0,
        CLKOUT4_PHASE      => 0.0,
        CLKOUT5_PHASE      => 0.0,
        CLKOUT6_PHASE      => 0.0,
        CLKOUT4_CASCADE    => FALSE,
        DIVCLK_DIVIDE      => 1,
        REF_JITTER1        => 0.0,
        STARTUP_WAIT       => FALSE)
    port map (
        CLKOUT0   => clk_out_int,
        CLKOUT0B  => open,
        CLKOUT1   => open,
        CLKOUT1B  => open,
        CLKOUT2   => open,
        CLKOUT2B  => open,
        CLKOUT3   => open,
        CLKOUT3B  => open,
        CLKOUT4   => open,
        CLKOUT5   => open,
        CLKOUT6   => open,
        CLKFBOUT  => clk_fb,
        CLKFBOUTB => open,
        LOCKED    => LOCKED,
        CLKIN1    => CLK_IN,
        PWRDWN    => '0',
        RST       => RST,
        CLKFBIN   => clk_fb);
end STRUCTURE;