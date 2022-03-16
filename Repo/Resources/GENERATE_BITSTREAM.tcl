start_gui
open_checkpoint $checkpoint_base
read_checkpoint -cell [get_cells DR] $checkpoint_proj
opt_design
place_design
route_design
write_bitstream -force ${project_name}.bit
stop_gui