common::send_msg_id "DRFront-011" "INFO" "Running a generate-bitstream script generated by DRFront..."
set script_mode $rdi::mode
if { $script_mode ne "gui" } {
  start_gui
}
set last_wd [ pwd ]
cd [ file dirname [ file normalize [ info script ] ] ]
if { [current_project -quiet] ne "" } {
  common::send_msg_id "DRFront-002" "INFO" "Closing currently opened project."
  while { [ current_sim -quiet ] ne ""} {
    close_sim
  }
  close_project
}

# Check if the checkpoint files exist
if { [ file exists $checkpoint_base ] == 0 } {
  common::send_msg_id "DRFront-012" "ERROR" "Base checkpoint file $checkpoint_base is not found."
}
set dcp_files [ lsort -dictionary [ glob -nocomplain *.dcp ] ]
set base_idx [ lsearch $dcp_files $checkpoint_base ]
set dcp_files [ lreplace $dcp_files $base_idx $base_idx ]
if { [ llength $dcp_files ] == 0 } {
  common::send_msg_id "DRFront-013" "ERROR" "Project checkpoint file is not found."
} elseif { [ llength $dcp_files ] == 1 } {
  set dcp_file [ lindex $dcp_files 0 ]
  if { $checkpoint_proj ne $dcp_file } {
    set checkpoint_proj $dcp_file
    common::send_msg_id "DRFront-014" "INFO" "$dcp_file is used as project checkpoint file."
  }
} else {
  set dcp_file [ lindex $dcp_files end ]
  set dcp_files [ lreplace $dcp_files end end ]
  file delete {*}$dcp_files
  file rename $dcp_file $checkpoint_proj
  common::send_msg_id "DRFront-015" "INFO" "$dcp_file is renamed to $checkpoint_proj and used as project checkpoint file."
}

# Main Part
open_checkpoint $checkpoint_base
read_checkpoint -cell [get_cells DR] $checkpoint_proj
opt_design
place_design
route_design
write_bitstream -force ${project_name}.bit
close_project

if { $script_mode ne "gui" } {
  stop_gui
}
cd $last_wd