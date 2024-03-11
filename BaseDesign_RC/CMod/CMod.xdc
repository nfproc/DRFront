## Constraint file for Arty A7 base design

## Clock signal
set_property -dict {PACKAGE_PIN L17 IOSTANDARD LVCMOS33} [get_ports CLK]
create_clock -period 83.333 -name sys_clk_pin -waveform {0.000 41.667} -add [get_ports CLK]

## USB-UART
set_property -dict {PACKAGE_PIN J18 IOSTANDARD LVCMOS33} [get_ports TXD]
set_property -dict {PACKAGE_PIN J17 IOSTANDARD LVCMOS33} [get_ports RXD]

## Switches
set_property -dict {PACKAGE_PIN K18 IOSTANDARD LVCMOS33} [get_ports {SW[0]}]
set_property -dict {PACKAGE_PIN J19 IOSTANDARD LVCMOS33} [get_ports {SW[1]}]
set_property -dict {PACKAGE_PIN H19 IOSTANDARD LVCMOS33} [get_ports {SW[2]}]
set_property -dict {PACKAGE_PIN H17 IOSTANDARD LVCMOS33} [get_ports {SW[3]}]

## LEDs
set_property -dict {PACKAGE_PIN G17 IOSTANDARD LVCMOS33} [get_ports {LD[0]}]
set_property -dict {PACKAGE_PIN G19 IOSTANDARD LVCMOS33} [get_ports {LD[1]}]
set_property -dict {PACKAGE_PIN N18 IOSTANDARD LVCMOS33} [get_ports {LD[2]}]
set_property -dict {PACKAGE_PIN L18 IOSTANDARD LVCMOS33} [get_ports {LD[3]}]
set_property -dict {PACKAGE_PIN B16 IOSTANDARD LVCMOS33} [get_ports {LD_BOARD[0]}]
set_property -dict {PACKAGE_PIN A17 IOSTANDARD LVCMOS33} [get_ports {LD_BOARD[1]}]
set_property -dict {PACKAGE_PIN C16 IOSTANDARD LVCMOS33} [get_ports {LD_BOARD[2]}]
set_property -dict {PACKAGE_PIN B17 IOSTANDARD LVCMOS33} [get_ports {LD_BOARD[3]}]
set_property -dict {PACKAGE_PIN C17 IOSTANDARD LVCMOS33} [get_ports {LD_BOARD[4]}]

## Buttons
set_property -dict {PACKAGE_PIN B18 IOSTANDARD LVCMOS33} [get_ports RST]
set_property -dict {PACKAGE_PIN A18 IOSTANDARD LVCMOS33} [get_ports BTNC]

set_property BITSTREAM.GENERAL.COMPRESS TRUE [current_design]
set_property BITSTREAM.CONFIG.CONFIGRATE 33 [current_design]
set_property CONFIG_MODE SPIx4 [current_design]

