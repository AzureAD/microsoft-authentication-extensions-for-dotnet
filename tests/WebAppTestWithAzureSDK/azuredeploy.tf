variable "location" {
    description = "Azure datacenter to deploy to."
    default = "westus2"
}

variable "resource_name_prefix" {
    description = "Input your unique Azure Service Bus Namespace name"
    default = "webapp"
}

variable "resource_group_name_prefix" {
    description = "Resource group to provision test infrastructure in."
    default = "web-app-tests"
}

resource "random_string" "name" {
    length  = 8
    upper   = false
    special = false
    number  = false
}

# Create resource group for all of the things
resource "azurerm_resource_group" "test" {
    name      = "${var.resource_group_name_prefix}-${random_string.name.result}"
    location  = "${var.location}"
}

resource "azurerm_app_service_plan" "test" {
    name                = "${var.resource_name_prefix}-plan-${random_string.name.result}"
    resource_group_name = "${azurerm_resource_group.test.name}"
    location            = "${var.location}"

    sku {
        tier = "Standard"
        size = "S1"
    }
}

resource "azurerm_virtual_network" "test" {
    name                = "${var.resource_name_prefix}-vnet-${random_string.name.result}"
    resource_group_name = "${azurerm_resource_group.test.name}"
    location            = "${var.location}"
    address_space       = ["10.0.0.0/16"]
}

resource "azurerm_subnet" "test" {
    name                    = "${var.resource_name_prefix}-subnet-${random_string.name.result}"
    resource_group_name     = "${azurerm_resource_group.test.name}"
    location                = "${azurerm_resource_group.test.location}"
    virtual_network_name    = "${azurerm_virtual_network.test.name}"
    address_prefix          = "10.0.2.0/24"
}

resource "azurerm_public_ip" "test_linux" {
    name                    = "${var.resource_name_prefix}-linux-pubip-${random_string.name.result}"
    resource_group_name     = "${azurerm_resource_group.test.name}"
    location                = "${azurerm_resource_group.test.location}"

    public_ip_address_allocation = "Dynamic"
}

resource "azurerm_network_interface" "test_linux" {
    name                = "${var.resource_name_prefix}-linux-nic-${random_string.name.result}"
    resource_group_name = "${azurerm_resource_group.test.name}"
    location            = "${azurerm_resource_group.test.location}"

    ip_configuration {
        name                          = "testconfiguration1"
        subnet_id                     = "${azurerm_subnet.test.id}"
        private_ip_address_allocation = "Dynamic"
        public_ip_address_id = "${azurerm_public_ip.test_linux.id}"
    }
}

resource "azurerm_virtual_machine" "test_linux" {
    name                    = "${var.resource_name_prefix}-linux-vm-${random_string.name.result}"
    location                = "${azurerm_resource_group.test.location}"
    resource_group_name     = "${azurerm_resource_group.test.name}"
    network_interface_ids   = ["${azurerm_network_interface.test_linux.id}"]
    vm_size                 = "Standard_DS1_v2"

    delete_os_disk_on_termination = true
    delete_data_disks_on_termination = true

    storage_image_reference {
        publisher = "credativ"
        offer     = "Debian"
        sku       = "9"
        version   = "latest"
    }
    storage_os_disk {
        name              = "osdisk"
        caching           = "ReadWrite"
        create_option     = "FromImage"
        managed_disk_type = "Premium_LRS"
    }
    os_profile {
        computer_name  = "hostname"
        admin_username = "testadmin"
    }
    os_profile_linux_config {
        disable_password_authentication = true
        ssh_keys {
            key_data = "${file("~/.ssh/id_rsa.pub")}"
            path = "/home/${azurerm_virtual_machine.test_linux.os_profile.admin_username}/.ssh/authorized_keys"
        }
    }
    identity {
        type = "SystemAssigned"
    }
}

resource "azurerm_public_ip" "test_win" {
    name                    = "${var.resource_name_prefix}-win-pubip-${random_string.name.result}"
    resource_group_name     = "${azurerm_resource_group.test.name}"
    location                = "${azurerm_resource_group.test.location}"

    public_ip_address_allocation = "Dynamic"
}

resource "azurerm_network_interface" "test_win" {
    name                = "${var.resource_name_prefix}-win-nic-${random_string.name.result}"
    resource_group_name = "${azurerm_resource_group.test.name}"
    location            = "${azurerm_resource_group.test.location}"

    ip_configuration {
        name                          = "testconfiguration1"
        subnet_id                     = "${azurerm_subnet.test.id}"
        private_ip_address_allocation = "Dynamic"
        public_ip_address_id = "${azurerm_public_ip.test_win.id}"
    }
}

resource "random_string" "secret" {
    length  = 32
    upper   = true
    special = true
    number  = true
}

resource "azurerm_virtual_machine" "test_win" {
    name                    = "${var.resource_name_prefix}-win-vm-${random_string.name.result}"
    location                = "${azurerm_resource_group.test.location}"
    resource_group_name     = "${azurerm_resource_group.test.name}"
    network_interface_ids   = ["${azurerm_network_interface.test_win.id}"]
    vm_size                 = "Standard_DS1_v2"

    delete_os_disk_on_termination = true
    delete_data_disks_on_termination = true

    storage_image_reference {
        publisher = "MicrosoftWindowsServer"
        offer     = "WindowsServer"
        sku       = "2019-Datacente"
        version   = "latest"
    }
    storage_os_disk {
        name              = "osdisk"
        caching           = "ReadWrite"
        create_option     = "FromImage"
        managed_disk_type = "Premium_LRS"
    }
    os_profile {
        computer_name  = "hostname"
        admin_username = "testadmin"
        admin_password = "${random_string.secret.result}"
    }
    identity {
        type = "SystemAssigned"
    }
}
