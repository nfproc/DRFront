-- DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
-- Copyright (C) 2022 Naoki FUJIEDA. New BSD License is applied.
------------------------------------------------------------------------

library IEEE;
use IEEE.std_logic_1164.ALL;

-- This is a blank, black-box circuit for reconfigurable module.
entity DR_TOP is
    port ( SW                             : in  std_logic_vector(15 downto 0);
           BTNC, BTNL, BTNR, BTNU, BTND   : in  std_logic;
           CLK, RST                       : in  std_logic;
           LD                             : out std_logic_vector(15 downto 0);
           AN                             : out std_logic_vector(7 downto 0);
           CA, CB, CC, CD, CE, CF, CG, DP : out std_logic);
end DR_TOP;

architecture STRUCTURE of DR_TOP is
    attribute black_box : string;
    attribute black_box of STRUCTURE : architecture is "yes";
begin

end STRUCTURE;

