using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Models;
using PontelloApp.Ultilities;
using System.Diagnostics;
using Product = PontelloApp.Models.Product;

namespace PontelloApp.Data
{
    public class PontelloAppInitializer
    {
        /// <summary>
        /// Prepares the Database and seeds data as required
        /// </summary>
        /// <param name="serviceProvider">DI Container</param>
        /// <param name="DeleteDatabase">Delete the database and start from scratch</param>
        /// <param name="UseMigrations">Use Migrations or EnsureCreated</param>
        /// <param name="SeedSampleData">Add optional sample data</param>
        public static void Initialize(IServiceProvider serviceProvider,
            bool DeleteDatabase = false, bool UseMigrations = true, bool SeedSampleData = true)
        {
            using (var context = new PontelloAppContext(
               serviceProvider.GetRequiredService<DbContextOptions<PontelloAppContext>>()))
            {
                //Refresh the database as per the parameter options
                #region Prepare the Database
                try
                {
                    //Note: .CanConnect() will return false if the database is not there!
                    if (DeleteDatabase || !context.Database.CanConnect())
                    {
                        if (!SqLiteDBUtility.ReallyEnsureDeleted(context)) //Delete the existing version 
                        {
                            Debug.WriteLine("Could not clear the old version " +
                                "of the database out of the way.  You will need to exit " +
                                "Visual Studio and try to do it manually.");
                        }

                        if (UseMigrations)
                        {
                            context.Database.Migrate(); //Create the Database and apply all migrations
                        }
                        else
                        {
                            context.Database.EnsureCreated(); //Create and update the database as per the Model
                        }
                        //Here is a good place to create any additional database objects such as Triggers or Views
                        //----------------------------------------------------------------------------------------
                        string sqlCmd = @"
                            CREATE TRIGGER SetProductTimestampOnUpdate
                            AFTER UPDATE ON Products
                            BEGIN
                                UPDATE Products
                                SET RowVersion = randomblob(8)
                                WHERE rowid = NEW.rowid;
                            END;
                        ";
                        context.Database.ExecuteSqlRaw(sqlCmd);

                        sqlCmd = @"
                            CREATE TRIGGER SetProductTimestampOnInsert
                            AFTER INSERT ON Products
                            BEGIN
                                UPDATE Products
                                SET RowVersion = randomblob(8)
                                WHERE rowid = NEW.rowid;
                            END
                        ";
                        context.Database.ExecuteSqlRaw(sqlCmd);

                        sqlCmd = @"
                            CREATE TRIGGER SetProductVariantTimestampOnUpdate
                            AFTER UPDATE ON ProductVariants
                            BEGIN
                                UPDATE ProductVariants
                                SET RowVersion = randomblob(8)
                                WHERE rowid = NEW.rowid;
                            END;
                        ";
                        context.Database.ExecuteSqlRaw(sqlCmd);

                        sqlCmd = @"
                            CREATE TRIGGER SetProductVariantTimestampOnInsert
                            AFTER INSERT ON ProductVariants
                            BEGIN
                                UPDATE ProductVariants
                                SET RowVersion = randomblob(8)
                                WHERE rowid = NEW.rowid;
                            END
                        ";
                        context.Database.ExecuteSqlRaw(sqlCmd);
                    }
                    else //The database is already created
                    {
                        if (UseMigrations)
                        {
                            context.Database.Migrate(); //Apply all migrations
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.GetBaseException().Message);
                }
                #endregion

                //Seed data needed
                #region Seed Required Data - Pontello
                try
                {
                    if (!context.Categories.Any())
                    {
                        // -------- Top-level Categories --------
                        var uncategorized = new Category { Name = "Uncategorized" };
                        var vehiclesAndParts = new Category { Name = "Vehicles & Parts" };
                        var hardware = new Category { Name = "Hardware" };
                        var apparel = new Category { Name = "Apparel" };
                        var other = new Category { Name = "Other" };

                        context.Categories.AddRange(uncategorized, vehiclesAndParts, hardware, apparel, other);
                        context.SaveChanges();

                        // -------- Vehicles & Parts --------
                        var vehiclePartsAccessories = new Category
                        {
                            Name = "Vehicle Parts & Accessories",
                            ParentCategoryID = vehiclesAndParts.ID
                        };
                        var vehicleStorageCargo = new Category
                        {
                            Name = "Vehicle Storage & Cargo",
                            ParentCategoryID = vehiclesAndParts.ID
                        };
                        var vehicleMaintenanceDecor = new Category
                        {
                            Name = "Vehicle Maintenance, Care & Decor",
                            ParentCategoryID = vehiclesAndParts.ID
                        };
                        var vehicleTowing = new Category
                        {
                            Name = "Vehicle Towing",
                            ParentCategoryID = vehiclesAndParts.ID
                        };
                        var vehicleDecor = new Category
                        {
                            Name = "Vehicle Decor",
                            ParentCategoryID = vehiclesAndParts.ID
                        };

                        context.Categories.AddRange(vehiclePartsAccessories, vehicleStorageCargo, vehicleMaintenanceDecor, vehicleTowing, vehicleDecor);
                        context.SaveChanges();

                        // -------- Vehicle Parts & Accessories Subcategories --------
                        var motorVehicleParts = new Category
                        {
                            Name = "Motor Vehicle Parts",
                            ParentCategoryID = vehiclePartsAccessories.ID
                        };
                        context.Categories.Add(motorVehicleParts);
                        context.SaveChanges();

                        // Motor Vehicle Parts → Suspension, Controls, Sensors & Gauges
                        var motorVehicleSuspension = new Category
                        {
                            Name = "Motor Vehicle Suspension Parts",
                            ParentCategoryID = motorVehicleParts.ID
                        };
                        var motorVehicleControls = new Category
                        {
                            Name = "Motor Vehicle Controls",
                            ParentCategoryID = motorVehicleParts.ID
                        };
                        var motorVehicleSensors = new Category
                        {
                            Name = "Motor Vehicle Sensors & Gauges",
                            ParentCategoryID = motorVehicleParts.ID
                        };
                        context.Categories.AddRange(motorVehicleSuspension, motorVehicleControls, motorVehicleSensors);
                        context.SaveChanges();

                        // Motor Vehicle Controls → Steering Racks, Wheels, Columns
                        context.Categories.AddRange(
                            new Category { Name = "Steering Racks", ParentCategoryID = motorVehicleControls.ID },
                            new Category { Name = "Steering Wheels", ParentCategoryID = motorVehicleControls.ID },
                            new Category { Name = "Steering Columns", ParentCategoryID = motorVehicleControls.ID }
                        );

                        // Motor Vehicle Frame & Body Parts
                        var motorVehicleFrameBody = new Category
                        {
                            Name = "Motor Vehicle Frame & Body Parts",
                            ParentCategoryID = motorVehicleParts.ID
                        };
                        context.Categories.Add(motorVehicleFrameBody);
                        context.SaveChanges();
                        context.Categories.Add(new Category { Name = "Bumpers", ParentCategoryID = motorVehicleFrameBody.ID });

                        // Motor Vehicle Transmission & Drivetrain
                        var motorVehicleDrivetrain = new Category
                        {
                            Name = "Motor Vehicle Transmission & Drivetrain Parts",
                            ParentCategoryID = motorVehicleParts.ID
                        };
                        context.Categories.Add(motorVehicleDrivetrain);
                        context.SaveChanges();
                        context.Categories.AddRange(
                            new Category { Name = "Flywheels", ParentCategoryID = motorVehicleDrivetrain.ID },
                            new Category { Name = "Axles", ParentCategoryID = motorVehicleDrivetrain.ID }
                        );

                        // Motor Vehicle Wheel Systems
                        var motorVehicleWheelSystem = new Category
                        {
                            Name = "Motor Vehicle Wheel Systems",
                            ParentCategoryID = motorVehicleParts.ID
                        };
                        context.Categories.Add(motorVehicleWheelSystem);
                        context.SaveChanges();
                        var motorVehicleTires = new Category
                        {
                            Name = "Motor Vehicle Tires",
                            ParentCategoryID = motorVehicleWheelSystem.ID
                        };
                        var motorVehicleRims = new Category
                        {
                            Name = "Motor Vehicle Rims & Wheels",
                            ParentCategoryID = motorVehicleWheelSystem.ID
                        };
                        context.Categories.AddRange(motorVehicleTires, motorVehicleRims);
                        context.SaveChanges();

                        context.Categories.Add(new Category
                        {
                            Name = "Off-Road and All-Terrain Vehicle Tires",
                            ParentCategoryID = motorVehicleTires.ID
                        });
                        context.Categories.Add(new Category
                        {
                            Name = "Off-Road and All-Terrain Vehicle Rims & Wheels",
                            ParentCategoryID = motorVehicleRims.ID
                        });

                        // -------- Vehicle Storage & Cargo --------
                        context.Categories.AddRange(
                            new Category { Name = "Motor Vehicle Carrying Racks", ParentCategoryID = vehicleStorageCargo.ID },
                            new Category { Name = "Vehicle Cargo Racks", ParentCategoryID = vehicleStorageCargo.ID }
                        );

                        // -------- Vehicle Maintenance / Fluids --------
                        var vehicleFluids = new Category { Name = "Vehicle Fluids", ParentCategoryID = vehicleMaintenanceDecor.ID };
                        context.Categories.Add(vehicleFluids);
                        context.SaveChanges();
                        context.Categories.AddRange(
                            new Category { Name = "Vehicle Motor Oil", ParentCategoryID = vehicleFluids.ID },
                            new Category { Name = "Vehicle Brake Fluid", ParentCategoryID = vehicleFluids.ID },
                            new Category { Name = "Vehicle Greases", ParentCategoryID = vehicleFluids.ID },
                            new Category { Name = "Vehicle Power Steering Fluid", ParentCategoryID = vehicleFluids.ID },
                            new Category { Name = "Vehicle Performance Additives", ParentCategoryID = vehicleFluids.ID },
                            new Category { Name = "Vehicle Engine Degreasers", ParentCategoryID = vehicleFluids.ID },
                            new Category { Name = "Vehicle Fuel System Cleaners", ParentCategoryID = vehicleFluids.ID },
                            new Category { Name = "Carburetor Cleaners", ParentCategoryID = vehicleFluids.ID },
                            new Category { Name = "Fuel Injector Cleaners", ParentCategoryID = vehicleFluids.ID }
                        );

                        context.Categories.AddRange(
                            new Category { Name = "Synthetic Motor Oil", ParentCategoryID = vehicleFluids.ID },
                            new Category { Name = "Conventional Motor Oil", ParentCategoryID = vehicleFluids.ID },
                            new Category { Name = "Semi-Synthetic Motor Oil", ParentCategoryID = vehicleFluids.ID }
                        );

                        // -------- Vehicle Towing --------
                        context.Categories.Add(new Category { Name = "Hitch Mounts", ParentCategoryID = vehicleTowing.ID });

                        // -------- Vehicle Decor --------
                        context.Categories.Add(new Category { Name = "Vehicle Decals", ParentCategoryID = vehicleDecor.ID });

                        // -------- Hardware --------
                        var hardwareFasteners = new Category { Name = "Hardware Fasteners", ParentCategoryID = hardware.ID };
                        var hardwareAccessories = new Category { Name = "Hardware Accessories", ParentCategoryID = hardware.ID };
                        context.Categories.AddRange(hardwareFasteners, hardwareAccessories);
                        context.SaveChanges();
                        context.Categories.AddRange(
                            new Category { Name = "Nuts & Bolts", ParentCategoryID = hardwareFasteners.ID },
                            new Category { Name = "Casters", ParentCategoryID = hardwareFasteners.ID },
                            new Category { Name = "Lubricants > Oil", ParentCategoryID = hardwareAccessories.ID }
                        );

                        // -------- Apparel --------
                        var clothingAccessories = new Category { Name = "Clothing Accessories", ParentCategoryID = apparel.ID };
                        context.Categories.Add(clothingAccessories);
                        context.SaveChanges();
                        context.Categories.AddRange(
                            new Category { Name = "Hats", ParentCategoryID = clothingAccessories.ID },
                            new Category { Name = "Trucker Hats", ParentCategoryID = clothingAccessories.ID },
                            new Category { Name = "Beanies", ParentCategoryID = clothingAccessories.ID }
                        );

                        // -------- Other --------
                        var cameras = new Category { Name = "Cameras & Optics", ParentCategoryID = other.ID };
                        context.Categories.Add(cameras);
                        context.SaveChanges();
                        var camerasSub = new Category { Name = "Cameras", ParentCategoryID = cameras.ID };
                        context.Categories.Add(camerasSub);
                        context.SaveChanges();
                        context.Categories.Add(new Category { Name = "Video Cameras", ParentCategoryID = camerasSub.ID });
                        context.Categories.Add(new Category { Name = "Media > Product Manuals", ParentCategoryID = other.ID });

                        context.SaveChanges();
                    }

                    // -------- Seed Vendors --------
                    if (!context.Vendors.Any())
                    {
                        context.Vendors.AddRange(
                            new Vendor { Name = "Charger Racing Chassis" },
                            new Vendor { Name = "Authentic Phantom Component" },
                            new Vendor { Name = "AiM Technology" },
                            new Vendor { Name = "Pontello Motorsports" },
                            new Vendor { Name = "Performance" },
                            new Vendor { Name = "Speed Karts" },
                            new Vendor { Name = "MCP" },
                            new Vendor { Name = "PMI" },
                            new Vendor { Name = "Franklin Signs" },
                            new Vendor { Name = "Uprinting" },
                            new Vendor { Name = "John's Kart Bodies" },
                            new Vendor { Name = "Chavous Racing Products" },
                            new Vendor { Name = "CHAVOUS" },
                            new Vendor { Name = "Berkebile Oil Company" },
                            new Vendor { Name = "Twenty5 Supply" },
                            new Vendor { Name = "Driven Racing Oil" },
                            new Vendor { Name = "HBM Reaper Tires" },
                            new Vendor { Name = "Van-K Wheels" },
                            new Vendor { Name = "PRC Carbon" },
                            new Vendor { Name = "Platinum Racing Chassis" },
                            new Vendor { Name = "Phantom Racing Chassis" },
                            new Vendor { Name = "Tilt Steering Wheel" },
                            new Vendor { Name = "MiniLite" },
                            new Vendor { Name = "NeXgen" },
                            new Vendor { Name = "Xcel Drive Systems" },
                            new Vendor { Name = "MEGA Mounts" },
                            new Vendor { Name = "Accutoe Advanced" }
                        );

                        context.SaveChanges();
                    }

                    // -------- Seed Products --------
                    if (!context.Products.Any())
                    {
                        // Vendor IDs
                        var uprintingId = context.Vendors.First(v => v.Name == "Uprinting").VendorID;
                        var chargerId = context.Vendors.First(v => v.Name == "Charger Racing Chassis").VendorID;
                        var aimId = context.Vendors.First(v => v.Name == "AiM Technology").VendorID;
                        var authenticId = context.Vendors.First(v => v.Name == "Authentic Phantom Component").VendorID;

                        // Category IDs
                        var catUncategorizedId = context.Categories.First(c => c.Name == "Uncategorized").ID;
                        var catBumpersId = context.Categories.First(c => c.Name == "Bumpers").ID;
                        var catMotorPartsId = context.Categories.First(c => c.Name == "Motor Vehicle Parts").ID;

                        context.Products.AddRange(
                            new Product //1
                            {
                                ProductName = "Ultra Racing Wheel",
                                Description = "High-performance racing wheel suitable for kart and small vehicles.",
                                IsActive = false,
                                CategoryID = catMotorPartsId,
                                Handle = "ultra-racing-wheel",
                                VendorID = chargerId,
                                Tag = "Racing Wheel"
                            },
                            new Product //2
                            {
                                ProductName = "Pedals",
                                Description = "The Pedals from Charger Racing Chassis is a durable, race-ready replacement designed for precise control and consistent feel on the track. Featuring a strong, lightweight construction and a smooth actuation profile, this pedal is built to withstand the demands of competitive karting while maintaining reliable performance season after season. Perfect for new builds, maintenance, or replacing worn components, these pedals install easily on compatible Charger chassis models and provides the responsiveness drivers expect. Application: Ideal for use on Charger Racing Chassis karts requiring a replacement or upgrade to accommodate drivers needs. Install as specified for throttle control systems to restore smooth, predictable driver input.",
                                IsActive = true,
                                CategoryID = catMotorPartsId,
                                Handle = "pedals",
                                VendorID = chargerId,
                                Tag = "Throttle & Brake Controls",
                                Type = "Throttle & Brake Controls"
                            },
                            new Product //3
                            {
                                ProductName = "Tie Rod",
                                Description = "The Tie Rod (11-3/4\") by Charger Racing Chassis is a durable steering linkage component designed to maintain accurate toe settings and consistent steering response. Built for reliable fitment and long-term durability, this tie rod helps transmit steering input smoothly while withstanding vibration and load encountered in competitive karting environments. Precision threading ensures repeatable adjustment and secure installation. Application: Used as part of the steering system on Charger and compatible Platinum chassis platforms to connect the steering yoke to the spindle arm. Ideal for steering system maintenance, alignment adjustment, or replacing worn tie rods. Suitable for Prodigy and Prodigy Cadet configurations—verify length and side orientation before installation.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "tie-rod",
                                VendorID = chargerId,
                                Tag = "Platinum, Prodigy, prodigy cadet, Steering Components, Tie Rods",
                                Type = "Steering Components"
                            },
                            new Product //4
                            {
                                ProductName = "Steering Toe Lock Kit",
                                Description = "The Steering Toe Lock Kit by Charger Racing Chassis is a precision steering component designed to securely lock toe settings and prevent unwanted adjustment during racing. Built for durability and repeatable fitment, this kit helps maintain consistent front-end alignment while withstanding vibration and steering loads. Its simple, effective design makes it an essential component for stable handling and reliable steering performance. Application: Used on Charger chassis steering systems to lock and retain toe alignment. Ideal for new builds, front-end setup tuning, or replacing worn or missing toe-lock hardware. Suitable for competitive karting applications where precise and repeatable steering geometry is required. Includes: (1) steering block, (1) steering lock, (1) pin and cable.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "steering-toe-lock-kit",
                                VendorID = chargerId,
                                Tag = "Steering Components",
                                Type = "Steering Components"
                            },
                            new Product //5
                            {
                                ProductName = "1/2\" ID Kingpin Washer",
                                Description = "The 1/2\" ID Kingpin Washer is a precision-machined component from Charger Racing Chassis, designed to provide proper spacing and alignment in kingpin assemblies. Crafted from durable materials, this washer ensures consistent performance, reduces wear, and maintains smooth spindle movement during high-stress racing conditions. It’s an essential part for chassis builders and racers maintaining top-level performance and reliability. Key Features: Precision Fit: Engineered for exact alignment within the kingpin assembly to maintain proper spindle spacing. Durable Material: Built to withstand heavy loads, impact, and vibration in competitive karting conditions. Consistent Performance: Reduces friction and wear while improving the longevity of spindle and kingpin components. Optimized Thickness: .060\" and 0.30\" designs ensures accurate spacing for smooth and responsive steering operation. Application: Install as specified in kart chassis assembly or during maintenance. Confirm correct sizing and fitment prior to installation. Compatible with Legacy, Legacy Cadet, Magnum, Prodigy, and Prodigy Cadet chassis, as well as other models using similar spindle and kingpin setups.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "1-2-id-kingpin-washer",
                                VendorID = chargerId,
                                IsUnlisted = true,
                                Tag = "Kingpin Items, Legacy, legacy cadet, Magnum, Prodigy, prodigy cadet, spindle, Spindle Items",
                                Type = "Spindle Items"
                            },
                            new Product //6
                            {
                                ProductName = "Fuel Tanks",
                                Description = "The Fuel Tanks from Charger Racing Chassis is a high-quality, race-ready fuel cell designed for reliability, safety, and performance. Built from durable materials to withstand the rigors of competitive karting, this fuel tank offers consistent fuel delivery and a compact design ideal for upright chassis mounting. Perfect for racers seeking efficient fuel storage and flow in tight spaces, it’s engineered for both ease of maintenance and long-term use. Key Features: ...",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "fuel-tank-3qt-up-right",
                                VendorID = chargerId,
                                Tag = "fuel, fuel can, fuel cell, Fuel Tanks, gas cell, gas tank",
                                Type = "Fuel Tanks"
                            },
                            new Product //7
                            {
                                ProductName = "Seat Saver Kit",
                                Description = "The Seat Saver Kit by Charger Racing Chassis is designed to reinforce fiberglass seat mounting points and extend seat life under regular racing use. These rivet-in washers replace standard 5/16\" flat washers, creating a stronger, more secure mounting surface that prevents bolt pull-through and minimizes wear around drilled holes. Built for durability and simplicity, this kit offers an easy, effective upgrade to protect your seat investment. Application: Used on fiberglass racing seats across Charger chassis models to reinforce mounting locations and maintain proper seat fitment. Ideal for new seat installations or refurbishing worn mounting holes. Rivets directly into the seat for a long-lasting, service-friendly solution.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "seat-saver-kit",
                                VendorID = chargerId,
                                Tag = "Bodywork, Dynasty, fiberglass, Legacy, legacy cadet, Magnum, Prodigy, prodigy cadet, Tyrant",
                                Type = "Seats & Accessories"
                            },
                            new Product //8
                            {
                                ProductName = "Rear Bumper Mounting Kit",
                                Description = "The Rear Bumper Mounting Kit from Charger Racing Chassis is a durable, track-ready hardware kit designed to securely mount rear bumpers across multiple Charger chassis models. Built to withstand the stress of competition, this kit uses high-quality fasteners to ensure a tight, reliable bumper installation that maintains structural support and safety during racing. Application: Install as specified for your Charger chassis during bumper setup or replacement. Ideal for routine maintenance, new builds, or replacing worn or damaged hardware. Always confirm sizing and chassis compatibility before installation. Compatible With: Charger, Legacy & Legacy Cadet, Magnum, Prodigy & Prodigy Cadet, Loop-Style Rear Bumpers.",
                                IsActive = true,
                                CategoryID = catBumpersId,
                                Handle = "2020-rear-bumper-mounting-kit",
                                VendorID = chargerId,
                                Tag = "charger, Legacy, legacy cadet, loop bumper, Magnum, Nerf Bars and Bumpers, Prodiggy, Prodigy, prodigy cadet",
                                Type = "Nerf Bars and Bumpers"
                            },
                            new Product //9
                            {
                                ProductName = "Rotary Throttle Position Sensor",
                                Description = "Rotary Design: The rotary-style throttle position sensor tracks the rotational movement of the throttle valve for precise throttle position measurements. Real-Time Data: Provides instant feedback on throttle inputs, helping teams analyze driver behavior and engine response. Durable: Built to withstand high temperatures, vibrations, and other harsh motorsport conditions. Ideal For: Motorsport Teams needing to monitor throttle inputs for performance tuning and driver analysis. Karting and Racing Teams or engineers needing to integrate throttle position data for vehicle tuning and driver performance improvement.",
                                IsActive = true,
                                CategoryID = catMotorPartsId,
                                Handle = "rotary-throttle-position-sensor",
                                VendorID = aimId,
                                Tag = "Sensors",
                                Type = "Electronic Hardware"
                            },
                            new Product //10
                            {
                                ProductName = "Floor Pan Bolt Kit",
                                Description = "The Floor Pan Bolt Kit from Charger Racing Chassis provides all the essential hardware required for securely mounting the floor pan to your kart chassis. Built from durable, corrosion-resistant materials, this kit ensures long-lasting reliability and easy installation. Each fastener is precision-made to deliver consistent fitment and performance in demanding racing environments. Key Features: Complete Mounting Kit: Includes all bolts, nuts, and washers necessary for floor pan installation. Durable Construction: High-quality steel hardware designed to resist vibration, wear, and corrosion. Easy Installation: Pre-sized components ensure a perfect fit with Charger Racing Chassis floor pans. Professional Finish: Provides a secure, clean look while maintaining proper chassis alignment. Package Contents: (13) Bolts, (13) Nuts, (13) Washers. Application: Install as specified for securing the floor pan to the kart chassis. Designed for Charger, Platinum, Prodigy, and Prodigy Cadet karts. Ideal for new builds, rebuilds, or maintenance replacements.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "floor-pan-bolt-kit",
                                VendorID = chargerId,
                                Tag = "Bodywork, fiberglass, Floor Pans, Platinum, Prodigy, prodigy cadet",
                                Type = "Bodywork"
                            },
                            new Product //11
                            {
                                ProductName = "Nerf Bar Pin Assembly",
                                Description = "The Nerf Bar Pin Assembly from Charger Racing Chassis is a complete hardware kit designed to secure the left or right nerf bar to your kart chassis. Each assembly is built with durable components to ensure a tight, reliable connection that can withstand the rigors of competitive racing. Ideal for both maintenance and new builds, this assembly makes nerf bar installation fast, secure, and repeatable. Key Features: Complete Hardware Kit: Includes all required components for one nerf bar side (left or right). High-Strength Construction: Precision-engineered pins and washers ensure long-lasting durability. Quick-Release Design: Safety pins allow for fast nerf bar removal and installation during setup or transport. Universal Fit: Compatible with both left and right side nerf bar mounts on Charger chassis. Race-Ready Durability: Designed to handle repeated impact and vibration under race conditions. Includes: (3) Clevis Pins, (3) Small Safety Pins, (6) Washers. Application: Used to mount and secure nerf bars on Charger Racing Chassis karts. Recommended for racers replacing worn hardware or assembling new bumpers and side bars.",
                                IsActive = true,
                                CategoryID = catMotorPartsId,
                                Handle = "nerf-bar-pin-assembly",
                                VendorID = chargerId,
                                Tag = "Nerf Bars and Bumpers",
                                Type = "Nerf Bars and Bumpers"
                            },
                            new Product //12
                            {
                                ProductName = "Pitman Arm Bolt Assembly",
                                Description = "The Pitman Arm Bolt Assembly from Charger Racing Chassis is a precision steering component designed to securely fasten the pitman arm to the steering system, ensuring smooth, consistent, and reliable steering performance. Built from high-quality hardware and engineered for durability, this assembly withstands the rigors of competitive karting and maintains proper steering geometry under load. Perfect for maintenance, rebuilds, and new chassis construction, this bolt assembly provides the secure connection required for accurate steering response. Included Components: (1) Pitman Arm Bolt, (1) Washer, (1) Lock Nut, (1) Safety Pin. Application: Install as specified on Charger steering systems where a pitman arm bolt is required. Ideal for replacing worn hardware or completing steering system assembly on Charger karts.",
                                IsActive = true,
                                CategoryID = catMotorPartsId,
                                Handle = "pitman-arm-bolt-assembly",
                                VendorID = chargerId,
                                Tag = "Steering Components",
                                Type = "Steering Components"
                            },
                            new Product //13
                            {
                                ProductName = "Brake Pedal",
                                Description = "Durable Construction: Designed to withstand the stresses of high-performance kart racing. Reverse Configuration: Ideal for karts with reverse braking systems. Comfortable Design: Provides secure grip for better control during braking. Precision Performance: Ensures consistent braking response in competitive conditions. Ideal For: Motorsport teams and karting enthusiasts needing a reliable brake pedal.",
                                IsActive = true,
                                CategoryID = catMotorPartsId,
                                Handle = "brake-pedal",
                                VendorID = authenticId,
                                Tag = "Brake Controls",
                                Type = "Brakes"
                            },
                            new Product //14
                            {
                                ProductName = "Axle Collar Set",
                                Description = "Description: The Axle Collar Set from Charger Racing Chassis provides secure positioning for your axle assembly, ensuring stability and proper alignment under high-performance conditions. Each set includes three precision-machined collars designed to hold the axle firmly in place, preventing lateral movement while maintaining smooth rotation. Ideal for assembly, maintenance, and replacement on all Charger chassis models. Key Features: Precision Machined: Ensures tight tolerance and consistent fit for maximum axle stability. Three-Collar Set: Includes three high-quality collars for even pressure and reliable retention. Durable Construction: Built from hardened steel or aluminum (depending on setup) for long-lasting performance. Enhanced Axle Security: Prevents unwanted side-to-side axle movement during intense race conditions. Application: Install as specified during axle assembly or service. Confirm axle diameter and proper fitment before installation. Compatible with Charger, Platinum, Prodigy, and Legacy chassis models using standard axle retention systems.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "axle-collar-set",
                                VendorID = authenticId,
                                Tag = "Axles & Components",
                                Type = "Axles & Components"
                            },
                            new Product //15
                            {
                                ProductName = "1/4 - 28 X 1-1/4\" Bullet End Stud",
                                Description = "The 1/4 - 28 x 1-1/4\" Bullet End Stud is a precision fastener from Charger Racing Chassis, designed for reliability and consistency in kart chassis and spindle assemblies. Featuring a bullet-style end for easier alignment during installation, this stud ensures secure fastening, durability, and ease of maintenance in competitive racing environments. Key Features: Bullet End Design: Simplifies installation by allowing quick thread alignment and reducing cross-threading. Precision Threading: The 1/4 - 28 fine thread provides a tight, secure fit to maintain consistent torque and clamping force. Durable Construction: Manufactured from high-grade materials to resist vibration, impact, and wear in demanding track conditions. Universal Fitment: Ideal for use across multiple chassis models, spindle assemblies, and steering components. Application: Install as specified for kart chassis or spindle assemblies. Verify correct size and thread pitch before installation. Commonly used in Legacy, Legacy Cadet, Magnum, Platinum, Prodigy, and Prodigy Cadet chassis models, as well as other karts utilizing 1/4 - 28 hardware in spindle and steering components.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "1-4-28-x-1-1-4-bullet-end-stud",
                                VendorID = chargerId,
                                Tag = "1/2 nuts, 1/4-28, 7/16 nuts, Axles & Components, Legacy, legacy cadet, lug nuts, lugs, Magnum, Platinum, Prodigy, prodigy cadet, Spindle Items, Steering Components, Wheel Hubs, wheel nuts",
                                Type = "Spindle Items"
                            },
                            new Product //16
                            {
                                ProductName = "Caster Block Safety Pin Assembly",
                                Description = "The Caster Block Safety Pin Assembly from Charger Racing Chassis is a precision-engineered safety component designed to secure the caster block assembly and prevent unwanted movement or separation during racing. Built for reliability, this assembly adds an extra layer of security to the front-end setup, ensuring consistent handling and durability under high-stress conditions. Key Features: Complete Hardware Set: Includes all necessary components for securing the caster block assembly. Enhanced Safety: Prevents caster block separation or loosening during operation. Durable Construction: High-quality steel and tether materials designed for repeated use and easy inspection. Simple Installation: Direct-fit design for Charger Racing Chassis front-end systems. Package Contents: (2) Caster Block Lower Bolts, (2) Washers, (2) Safety Pins, (2) Tether Wires. Application: Install as specified to secure caster block assemblies on Charger Racing Chassis front-end systems. Confirm correct bolt sizing and fitment before installation. Compatible with Prodigy, Legacy, Magnum, and other Charger chassis utilizing standard front spindle and caster block configurations.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "caster-block-safety-pin-assembly",
                                VendorID = chargerId,
                                Tag = "Camber Components, Caster Components, spindle, Spindle Items, Steering Components",
                                Type = "Steering Components"
                            },
                            new Product //17
                            {
                                ProductName = "Lightweight Aluminum 5/8\" Spindle Nut",
                                Description = "The Lightweight Aluminum 5/8\" Spindle Nut Set (2) from Charger Racing Chassis is designed for racers seeking weight savings without sacrificing strength or reliability. Precision-machined from high-grade aluminum, these spindle nuts reduce rotating mass and improve front-end response, making them ideal for competitive karting applications. Their durable silver anodized finish provides both corrosion resistance and a clean, professional appearance. Key Features: Lightweight Design: Aluminum construction reduces unsprung weight for improved handling and steering feel. Precision Machined: Ensures a tight, accurate fit for reliable spindle assembly performance. Corrosion-Resistant Finish: Silver anodized coating protects against oxidation and wear. Set of Two: Supplied as a matched pair for complete spindle installation. Race-Proven Durability: Built to withstand the rigors of speedway and oval kart racing. Application: Used to secure front spindle assemblies on Charger, Prodigy, and Platinum chassis. Ideal for racers upgrading from standard steel nuts to lighter, performance-focused components.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "lightweight-aluminum-5-8-spindle-nut",
                                VendorID = chargerId,
                                Tag = "Spindle Items",
                                Type = "Spindle Items"
                            },
                            new Product //18
                            {
                                ProductName = "Steering Yoke with Nut",
                                Description = "The Steering Yoke with Nut by Charger Racing Chassis is a threaded steering linkage component designed to provide a secure and adjustable connection within the kart steering system. Precision-machined for consistent fitment, this yoke assembly allows accurate steering alignment while maintaining strength under racing loads. Supplied with the matching retaining nut, it offers a complete and service-ready solution for steering system assembly or replacement. Application: Used as part of the steering linkage on Charger chassis platforms to connect steering components and fine-tune steering geometry. Ideal for new builds, steering repairs, or replacing worn threaded yokes. Suitable for competitive karting applications where reliable steering response and adjustment capability are required.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "steering-yoke-with-nut",
                                VendorID = chargerId,
                                Tag = "Steering Components",
                                Type = "Steering Components"
                            },
                            new Product //19
                            {
                                ProductName = "Quick Release 5/8\" Steering Wheel Hub for Champ",
                                Description = "The Quick Release 5/8\" Steering Wheel Hub for Champ from Charger Racing Chassis is a precision-engineered steering component designed to deliver fast, secure, and reliable wheel removal during kart setup, transport, and maintenance. Built to withstand demanding race environments, this hub provides a smooth locking action and consistent performance, ensuring the wheel stays firmly in place while still offering effortless release when needed. Constructed with durable, race-grade materials, it’s an essential upgrade for drivers and teams seeking convenience, safety, and efficiency in Champ kart applications. Key Features: Quick-Release Function: Allows rapid removal and attachment of the steering wheel—ideal for pit adjustments, driver changes, or tight cockpit entry/exit. 5/8\" Fitment: Designed specifically for 5/8\" steering shafts used on Champ-style karts. Precision-Machined: Ensures a tight, reliable connection with smooth engagement every time. Durable Construction: Built to endure high-stress racing conditions without loosening or premature wear. Application: Install as specified for Champ kart steering systems. Ensure proper shaft size and steering wheel bolt pattern before installation.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "quick-release-5-8-steering-wheel-hub-for-champ",
                                VendorID = chargerId,
                                Tag = "Steering Components",
                                Type = "Steering Components"
                            },
                            new Product //20
                            {
                                ProductName = "Lower Steering Upright Bolt Assembly",
                                Description = "The Lower Steering Upright Bolt Assembly from Charger Racing Chassis is a complete, race-ready hardware set designed to secure the Steering shaft to the tie rods. Each component is precision-manufactured to maintain alignment and ensure reliable steering performance under high-stress racing conditions. The included drilled Allen bolt allows for the use of safety wire, providing additional security during competition. Key Features: Complete Hardware Set: Contains all required components for secure upright installation. Drilled for Safety Wire: Prevents loosening due to vibration or impact. Precision-Engineered Fit: Designed specifically for Charger Racing Chassis front-end systems. Durable Construction: Made from high-strength materials for long-term reliability. Race-Proven Design: Ensures consistent steering performance and safety in competition. Included in Kit: (1) Drilled Allen Bolt, (1) Washer, (1) Nut, (1) Safety Pin. Application: Used to fasten the steering shaft to the tie rods on Charger Racing Chassis karts. Recommended for steering rebuilds, chassis maintenance, or OEM-spec assembly where precision and safety are critical.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "single-steering-upright-bolt-assembly",
                                VendorID = chargerId,
                                Tag = "Steering Components",
                                Type = "Steering Components"
                            },
                            new Product //21
                            {
                                ProductName = "Wheel Hub Spacer",
                                Description = "The Wheel Hub Spacer by Charger Racing Chassis is a precision-machined spacing component designed to fine-tune wheel hub positioning on the spindle. Manufactured for consistent thickness and reliable fitment, this spacer allows accurate adjustment while maintaining proper bearing alignment and smooth wheel rotation. Its durable construction makes it suitable for repeated service and race-day adjustments. Application: Used on front spindle assemblies to space wheel hubs as needed for alignment, clearance, or setup tuning. Ideal for chassis setup changes, maintenance, or replacing worn spacers. Compatible with 5/8\" spindle applications—verify thickness and fitment requirements before installation.",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "1-4-x-5-8-wheel-hub-spacer",
                                VendorID = chargerId,
                                Tag = "Spindle Items",
                                Type = "Spindle Items"
                            },
                            new Product //22
                            {
                                ProductName = "1/2 Kingpin Washer - 0.30\" Thin",
                                Description = "1/2 Size: Specifically designed for 1/2 kingpins, providing the correct fit and spacing. 0.30\" Thin Thickness: A thin washer designed to add minimal height while still providing stable alignment. Durable and Lightweight: Made from high-strength materials that are lightweight and built to endure high-speed karting. Ideal For: Motorsport Teams needing a thin washer for precise kingpin alignment. Karting Enthusiasts looking to maintain proper spacing in steering assemblies with minimal height increase.",
                                IsActive = true,
                                CategoryID = catMotorPartsId,
                                Handle = "1-2-kingpin-washer-030-thin",
                                VendorID = authenticId,
                                Tag = "Kingpins",
                                Type = "Kingpins"
                            },
                            new Product //23
                            {
                                ProductName = "2019 Vintage Short-Sleeve Unisex T-Shirt",
                                Description = "This t-shirt is everything you've dreamed of and more. It feels soft and lightweight, with the right amount of stretch. It\'s comfortable and flattering for both men and women. 100% combed and ring-spun cotton (heather colors contain polyester) Fabric weight: 4.2 oz (142 g/m2) Shoulder-to-shoulder taping Side-seamed The Male model is wearing a size M. He's 6.2 feet (190 cm) tall, chest circumference 37.7\" (96 cm), waist circumference 33.4\" (85 cm). The female model is wearing a size M. She\'s 5.8 feet (178 cm) tall, chest circumference 34.6\" (88 cm), waist circumference 27.16\" (69 cm), hip circumference 37.7",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                IsUnlisted = true,
                                Handle = "2019-vintage-short-sleeve-unisex-t-shirt",
                                VendorID = chargerId,
                                Tag = "charger, prodigy, prodigy cadet",
                                Type = ""
                            },
                            new Product //24
                            {
                                ProductName = "JKB Fiberglass Seat",
                                Description = "The JKB Fiberglass Seat provides a durable, lightweight, and performance-driven seating solution for kart racers. Designed to deliver optimal driver support and comfort, the JKB seat combines strength with ergonomic shaping for improved control and consistency on track. Its fiberglass construction offers a balance of rigidity and flexibility, making it ideal for both junior and adult karting applications. Key Features: \r\n Lightweight Fiberglass Construction: Ensures reduced overall kart weight without compromising durability.\r\nErgonomic Design: Contoured to enhance driver comfort and maintain a stable seating position under cornering loads.\r\nRace-Proven Durability: Built to withstand impacts and stress from competitive racing.\r\nHigh-Quality Finish: Smooth, clean surface ready for direct mounting or seat padding installation.\r\nAvailable Sizes: Offered in multiple sizes, including Rookie Junior, and Senior, to suit a wide range of drivers.\r\nApplication: Ideal for Charger Platinum, and Prodigy chassis karts. Recommended for both youth and adult racers seeking a professional-grade seat that balances comfort, control, and performance.\r\n",
                                IsActive = true,
                                CategoryID = catUncategorizedId,
                                Handle = "fiberglass-seat",
                                VendorID = chargerId,
                                Tag = "Seats & Accessories",
                                Type = "Seats & Accessories"
                            }
                        );

                        context.SaveChanges();
                    }

                    // -------- Seed Variant --------
                    if (!context.ProductVariants.Any())
                    {
                        context.ProductVariants.AddRange(
                            new ProductVariant //1
                            {
                                ProductId = 1,
                                UnitPrice = 99.99m,
                                StockQuantity = 20,
                                SKU_ExternalID = "URW-001",
                                InventoryPolicy = InventoryPolicy.Continue,
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "Small" },
                                        new Variant { Name = "Color", Value = "Red" }
                                    }
                            },
                                new ProductVariant //1
                                {
                                    ProductId = 1,
                                    UnitPrice = 109.99m,
                                    StockQuantity = 15,
                                    SKU_ExternalID = "URW-002",
                                    InventoryPolicy = InventoryPolicy.Continue,
                                    Status = true,
                                    Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "Small" },
                                        new Variant { Name = "Color", Value = "Blue" }
                                    }
                                },
                                new ProductVariant //1
                                {
                                    ProductId = 1,
                                    UnitPrice = 119.99m,
                                    StockQuantity = 10,
                                    SKU_ExternalID = "URW-003",
                                    InventoryPolicy = InventoryPolicy.Deny,
                                    Status = true,
                                    Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "Large" },
                                        new Variant { Name = "Color", Value = "Red" }
                                    }
                                },
                                new ProductVariant //1
                                {
                                    ProductId = 1,
                                    UnitPrice = 129.99m,
                                    StockQuantity = 5,
                                    SKU_ExternalID = "URW-004",
                                    InventoryPolicy = InventoryPolicy.Deny,
                                    Status = false,
                                    Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "Large" },
                                        new Variant { Name = "Color", Value = "Blue" }
                                    }
                                },
                            new ProductVariant //2
                            {
                                ProductId = 2,
                                UnitPrice = 33.90m,
                                StockQuantity = 20,
                                SKU_ExternalID = "1170",
                                CostPrice = 23.39m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 453.59237m,
                                Unit = ImperialUnits.lb,
                                Barcode = "TP1170",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Type", Value = "Throttle Pedal" }
                                    }
                            },
                            new ProductVariant //2
                            {
                                ProductId = 2,
                                UnitPrice = 39.55m,
                                StockQuantity = 22,
                                SKU_ExternalID = "1171",
                                CostPrice = 23.39m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 453.59237m,
                                Unit = ImperialUnits.lb,
                                Barcode = "TP1171",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Type", Value = "Reverse Throttle Pedal" }
                                    }
                            },
                            new ProductVariant //2
                            {
                                ProductId = 2,
                                UnitPrice = 33.90m,
                                StockQuantity = 22,
                                SKU_ExternalID = "1182",
                                CostPrice = 33.90m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 453.59237m,
                                Unit = ImperialUnits.lb,
                                Barcode = "BP1182",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Type", Value = "Brake Pedal" }
                                    }
                            },
                            new ProductVariant //2
                            {
                                ProductId = 2,
                                UnitPrice = 39.55m,
                                StockQuantity = 22,
                                SKU_ExternalID = "1183",
                                CostPrice = 39.55m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 453.59237m,
                                Unit = ImperialUnits.lb,
                                Barcode = "BP1183",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Type", Value = "Reverse Brake Pedal" }
                                    }
                            },
                            new ProductVariant //3
                            {
                                ProductId = 3,
                                UnitPrice = 14.3m,
                                StockQuantity = 42,
                                SKU_ExternalID = "1075",
                                CostPrice = 5.72m,
                                InventoryPolicy = InventoryPolicy.Continue,
                                Weight = 453.59237m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Type", Value = "6 1/2\" Tie Rod Left" }
                                    }
                            },
                            new ProductVariant //3
                            {
                                ProductId = 3,
                                UnitPrice = 14.3m,
                                StockQuantity = 31,
                                SKU_ExternalID = "1074",
                                CostPrice = 5.72m,
                                InventoryPolicy = InventoryPolicy.Continue,
                                Weight = 453.59237m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Type", Value = "11 3/4\" Tie Rod Right" }
                                    }
                            },
                            new ProductVariant //4
                            {
                                ProductId = 4,
                                UnitPrice = 37.29m,
                                StockQuantity = 12,
                                SKU_ExternalID = "1092",
                                CostPrice = 18.63m,
                                InventoryPolicy = InventoryPolicy.Continue,
                                Weight = 907.18474m,
                                Unit = ImperialUnits.lb,
                                Barcode = "TL1092J",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //5
                            {
                                ProductId = 5,
                                UnitPrice = 0.77m,
                                StockQuantity = 7,
                                SKU_ExternalID = "1100",
                                CostPrice = 0.77m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 113.3980925m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "'-.060\" Thick" }
                                    }
                            },
                            new ProductVariant //5
                            {
                                ProductId = 5,
                                UnitPrice = 0.77m,
                                StockQuantity = 11,
                                SKU_ExternalID = "1101",
                                CostPrice = 0.77m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 113.3980925m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "'-.030\" Thin" }
                                    }
                            },
                             new ProductVariant //6
                             {
                                 ProductId = 6,
                                 UnitPrice = 79.1m,
                                 StockQuantity = 20,
                                 SKU_ExternalID = "3fuel",
                                 CostPrice = 37.35m,
                                 InventoryPolicy = InventoryPolicy.Deny,
                                 Weight = 453.59237m,
                                 Unit = ImperialUnits.lb,
                                 Barcode = "",
                                 Status = true,
                                 Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "3 Qt" }
                                    }
                             },
                            new ProductVariant //6
                            {
                                ProductId = 6,
                                UnitPrice = 84.75m,
                                StockQuantity = 10,
                                SKU_ExternalID = "5fuel",
                                CostPrice = 84.75m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 907.1847m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "5 Qt" }
                                    }
                            },
                            new ProductVariant //6
                            {
                                ProductId = 6,
                                UnitPrice = 169.5m,
                                StockQuantity = 23,
                                SKU_ExternalID = "7fuel",
                                CostPrice = 169.5m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 3175.14659m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "7 Qt Floor Mounted" }
                                    }
                            },
                            new ProductVariant //6
                            {
                                ProductId = 6,
                                UnitPrice = 28.25m,
                                StockQuantity = 3,
                                SKU_ExternalID = "2fuel",
                                CostPrice = 28.25m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 453.59237m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "2 Qt Floor Mounted" }
                                    }
                            },
                            new ProductVariant //7
                            {
                                ProductId = 7,
                                UnitPrice = 13.56m,
                                StockQuantity = 19,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //8
                            {
                                ProductId = 8,
                                UnitPrice = 31.92m,
                                StockQuantity = 27,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 453.59237m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //9
                            {
                                ProductId = 9,
                                UnitPrice = 202.5m,
                                StockQuantity = 21,
                                SKU_ExternalID = "AiM-X05SNRP972",
                                CostPrice = 202.5m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Barcode = "52718556",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //10
                            {
                                ProductId = 10,
                                UnitPrice = 7.15m,
                                StockQuantity = 20,
                                SKU_ExternalID = "1223",
                                CostPrice = 7.15m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 45.359237m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //11
                            {
                                ProductId = 11,
                                UnitPrice = 8.19m,
                                StockQuantity = 33,
                                SKU_ExternalID = "1070",
                                CostPrice = 88.15m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 226.796185m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //12
                            {
                                ProductId = 12,
                                UnitPrice = 6.5m,
                                StockQuantity = 28,
                                SKU_ExternalID = "1085",
                                CostPrice = 6.5m,
                                InventoryPolicy = InventoryPolicy.Continue,
                                Weight = 226.796185m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //13
                            {
                                ProductId = 13,
                                UnitPrice = 44.55m,
                                StockQuantity = 19,
                                SKU_ExternalID = "PRC-1125100",
                                CostPrice = 44.55m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Barcode = "24943068",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Option", Value = "Regular" }
                                    }
                            },
                            new ProductVariant //13
                            {
                                ProductId = 13,
                                UnitPrice = 44.55m,
                                StockQuantity = 31,
                                SKU_ExternalID = "PRC-1125100R",
                                CostPrice = 44.55m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Barcode = "18094044",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Option", Value = "Reverse" }
                                    }
                            },
                            new ProductVariant //14
                            {
                                ProductId = 14,
                                UnitPrice = 27.12m,
                                StockQuantity = 7,
                                SKU_ExternalID = "1153",
                                CostPrice = 3.5m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 453.59237m,
                                Unit = ImperialUnits.lb,
                                Barcode = "AX1153",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //15
                            {
                                ProductId = 15,
                                UnitPrice = 2.26m,
                                StockQuantity = 27,
                                SKU_ExternalID = "1147",
                                CostPrice = 2.26m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 113.3980925m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //16
                            {
                                ProductId = 16,
                                UnitPrice = 15.59m,
                                StockQuantity = 18,
                                SKU_ExternalID = "1137",
                                CostPrice = 15.59m,
                                InventoryPolicy = InventoryPolicy.Continue,
                                Weight = 226.796185m,
                                Unit = ImperialUnits.lb,
                                Barcode = "CA1137",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //17
                            {
                                ProductId = 17,
                                UnitPrice = 11.3m,
                                StockQuantity = 22,
                                SKU_ExternalID = "1106",
                                CostPrice = 11.3m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 113.3980925m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Color", Value = "Silver" }
                                    }
                            },
                            new ProductVariant //18
                            {
                                ProductId = 18,
                                UnitPrice = 16.89m,
                                StockQuantity = 39,
                                SKU_ExternalID = "1095",
                                CostPrice = 16.89m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 453.59237m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //19
                            {
                                ProductId = 19,
                                UnitPrice = 57.18m,
                                StockQuantity = 9,
                                SKU_ExternalID = "1091",
                                CostPrice = 24.21m,
                                InventoryPolicy = InventoryPolicy.Continue,
                                Weight = 2267.96185m,
                                Unit = ImperialUnits.lb,
                                Barcode = "QR1091L",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                            new ProductVariant //20
                            {
                                ProductId = 20,
                                UnitPrice = 5.65m,
                                StockQuantity = 28,
                                SKU_ExternalID = "1086",
                                CostPrice = 5.65m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 113.3980925m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    }
                            },
                             new ProductVariant //21
                             {
                                 ProductId = 21,
                                 UnitPrice = 1.25m,
                                 StockQuantity = 11,
                                 SKU_ExternalID = "1109",
                                 CostPrice = 0.57m,
                                 InventoryPolicy = InventoryPolicy.Deny,
                                 Weight = 113.3980925m,
                                 Unit = ImperialUnits.lb,
                                 Barcode = "",
                                 Status = true,
                                 Options = new List<Variant>
                                    {
                                        new Variant { Name = "Axle Size", Value = "1/8 thick x 5/8" },
                                    }
                             },
                            new ProductVariant //21
                            {
                                ProductId = 21,
                                UnitPrice = 1.25m,
                                StockQuantity = 6,
                                SKU_ExternalID = "1110",
                                CostPrice = 0.57m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 113.3980925m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Axle Size", Value = "18 thick x3/4" },
                                    }
                            },
                           new ProductVariant //22
                           {
                               ProductId = 22,
                               UnitPrice = 0.9m,
                               StockQuantity = 16,
                               SKU_ExternalID = "PRC-1111019T",
                               CostPrice = 0.9m,
                               InventoryPolicy = InventoryPolicy.Deny,
                               Weight = 0.008454m,
                               Unit = ImperialUnits.oz,
                               Barcode = "56420316",
                               Status = true,
                               Options = new List<Variant>
                                    {
                                        new Variant { Name = "Title", Value = "Default" }
                                    },
                           },
                           new ProductVariant //23
                           {
                               ProductId = 23,
                               UnitPrice = 22.5m,
                               StockQuantity = 4,
                               SKU_ExternalID = "3726894",
                               CostPrice = 11.5m,
                               InventoryPolicy = InventoryPolicy.Continue,
                               Weight = 130.4078064m,
                               Unit = ImperialUnits.oz,
                               Barcode = "",
                               Status = true,
                               Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "Small" },
                                        new Variant { Name = "Color", Value = "Forest" }
                                    }
                           },
                            new ProductVariant //23
                            {
                                ProductId = 23,
                                UnitPrice = 21.5m,
                                StockQuantity = 2,
                                SKU_ExternalID = "3726811",
                                CostPrice = 11.5m,
                                InventoryPolicy = InventoryPolicy.Continue,
                                Weight = 130.4078064m,
                                Unit = ImperialUnits.oz,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Size", Value = "Medium" },
                                        new Variant { Name = "Color", Value = "Forest" }
                                    }
                            },
                            new ProductVariant //24
                            {
                                ProductId = 24,
                                UnitPrice = 129.95m,
                                StockQuantity = 1,
                                SKU_ExternalID = "",
                                CostPrice = 129.95m,
                                CompareAtPrice = 109.99m,
                                InventoryPolicy = InventoryPolicy.Deny,
                                Weight = 2267.96185m,
                                Unit = ImperialUnits.lb,
                                Barcode = "",
                                Status = true,
                                Options = new List<Variant>
                                    {
                                        new Variant { Name = "Style", Value = "Evolution" },
                                        new Variant { Name = "Size", Value = "Small" }
                                    }
                            }
                            );
                        context.SaveChanges();
                    }

                    var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
                    var dealer = userManager.Users.FirstOrDefault(u => u.Email == "dealer@gmail.com");

                    // -------- Seed Sample (Progress) Orders --------
                    if (!context.Orders.Any())
                    {
                        context.Orders.AddRange(
                            // -------- Submitted Orders --------
                            new Order
                            {
                                PONumber = "PO-20260313145735",
                                UserId = dealer.Id,
                                Status = OrderStatus.Submitted,
                                CreatedAt = new DateTime(2026, 3, 17, 9, 15, 0),
                                TotalAmount = 0.90m,
                                TaxAmount = 0.12m,
                                Shipping = new Shipping
                                {
                                    FullName = "Ally Smith",
                                    StreetAddress = "25 Sunset Way",
                                    City = "Niagara Falls",
                                    Province = "Ontario",
                                    Country = "Canada",
                                    PostalCode = "L2J 1N1",
                                    Phone = "289-456-7810",
                                    Email = "allysmith@gmail.com"
                                },
                                Items = new List<OrderItem>
                                {
                new OrderItem { ProductId = 2, ProductVariantId = 5, Quantity = 1, UnitPrice = 0.90m }
                                }
                            },

                            new Order
                            {
                                PONumber = "PO-20260203145730",
                                UserId = dealer.Id,
                                Status = OrderStatus.Submitted,
                                CreatedAt = new DateTime(2026, 3, 18, 10, 30, 0),
                                TotalAmount = 14.30m,
                                TaxAmount = 1.86m,
                                Shipping = new Shipping
                                {
                                    FullName = "Ella Jones",
                                    StreetAddress = "345 Stone Road",
                                    City = "Welland",
                                    Province = "Ontario",
                                    Country = "Canada",
                                    PostalCode = "L9N 1H2",
                                    Phone = "289-749-2345",
                                    Email = "ellajones@gmail.com"
                                },
                                Items = new List<OrderItem>
                                {
                new OrderItem { ProductId = 3, ProductVariantId = 9, Quantity = 1, UnitPrice = 14.30m }
                                }
                            },

                            // -------- Approved Orders --------
                            new Order
                            {
                                PONumber = "PO-20260113145725",
                                UserId = dealer.Id,
                                Status = OrderStatus.Approved,
                                CreatedAt = new DateTime(2026, 3, 10, 14, 45, 0),
                                TotalAmount = 37.29m,
                                TaxAmount = 4.85m,
                                Shipping = new Shipping
                                {
                                    FullName = "Emma Smith",
                                    StreetAddress = "365 Velvet Rd",
                                    City = "Niagara Falls",
                                    Province = "Ontario",
                                    Country = "Canada",
                                    PostalCode = "L2C 3N8",
                                    Phone = "289-365-8374",
                                    Email = "emmasmith@hotmail.com"
                                },
                                Items = new List<OrderItem>
                                {
                new OrderItem { ProductId = 4, ProductVariantId = 11, Quantity = 1, UnitPrice = 37.29m }
                                }
                            },

                            new Order
                            {
                                PONumber = "PO-20260313145720",
                                UserId = dealer.Id,
                                Status = OrderStatus.Approved,
                                CreatedAt = new DateTime(2026, 3, 12, 11, 0, 0),
                                TotalAmount = 4.62m,
                                TaxAmount = 0.60m,
                                Shipping = new Shipping
                                {
                                    FullName = "Lucas Jones",
                                    StreetAddress = "47 Merrit Ave",
                                    City = "St Catharines",
                                    Province = "Ontario",
                                    Country = "Canada",
                                    PostalCode = "L1J 9I3",
                                    Phone = "905-397-4836",
                                    Email = "lucasjones@gmail.com"
                                },
                                Items = new List<OrderItem>
                                {
                new OrderItem { ProductId = 5, ProductVariantId = 12, Quantity = 6, UnitPrice = 0.77m }
                                }
                            },

                            // -------- Rejected Orders --------
                            new Order
                            {
                                PONumber = "PO-20260313145715",
                                UserId = dealer.Id,
                                Status = OrderStatus.Rejected,
                                CreatedAt = new DateTime(2026, 3, 15, 16, 0, 0),
                                TotalAmount = 79.10m,
                                TaxAmount = 10.28m,
                                Shipping = new Shipping
                                {
                                    FullName = "Jimmy White",
                                    StreetAddress = "291 Portrage Road",
                                    City = "Thorold",
                                    Province = "Ontario",
                                    Country = "Canada",
                                    PostalCode = "L2J 2C2",
                                    Phone = "931-323-9239",
                                    Email = "jimmy@gmail.com"
                                },
                                Items = new List<OrderItem>
                                {
                new OrderItem { ProductId = 6, ProductVariantId = 15, Quantity = 1, UnitPrice = 79.10m }
                                }
                            },

                            new Order
                            {
                                PONumber = "PO-20260313145710",
                                UserId = dealer.Id,
                                Status = OrderStatus.Rejected,
                                CreatedAt = new DateTime(2026, 3, 16, 12, 30, 0),
                                TotalAmount = 13.56m,
                                TaxAmount = 1.76m,
                                Shipping = new Shipping
                                {
                                    FullName = "Ava Smith",
                                    StreetAddress = "632 Oneil Street",
                                    City = "Niagara Falls",
                                    Province = "Ontario",
                                    Country = "Canada",
                                    PostalCode = "L1J 8K1",
                                    Phone = "905-319-3204",
                                    Email = "smith@gmail.com"
                                },
                                Items = new List<OrderItem>
                                {
                new OrderItem { ProductId = 7, ProductVariantId = 18, Quantity = 1, UnitPrice = 13.56m }
                                }
                            },

                            // -------- Shipped Orders --------
                            new Order
                            {
                                PONumber = "PO-20260313145770",
                                UserId = dealer.Id,
                                Status = OrderStatus.Shipped,
                                CreatedAt = new DateTime(2026, 3, 20, 9, 0, 0),
                                TotalAmount = 31.92m,
                                TaxAmount = 4.15m,
                                Shipping = new Shipping
                                {
                                    FullName = "Tony Smith",
                                    StreetAddress = "100 Parkside Way",
                                    City = "Welland",
                                    Province = "Ontario",
                                    Country = "Canada",
                                    PostalCode = "L3J 2L9",
                                    Phone = "905-394-3875",
                                    Email = "tonysmith@outlook.com"
                                },
                                Items = new List<OrderItem>
                                {
                new OrderItem { ProductId = 8, ProductVariantId = 19, Quantity = 1, UnitPrice = 31.92m }
                                }
                            },

                            new Order
                            {
                                PONumber = "PO-20260313145760",
                                UserId = dealer.Id,
                                Status = OrderStatus.Shipped,
                                CreatedAt = new DateTime(2026, 3, 21, 10, 15, 0),
                                TotalAmount = 202.50m,
                                TaxAmount = 26.33m,
                                Shipping = new Shipping
                                {
                                    FullName = "Sam Jones",
                                    StreetAddress = "85 Autumn Ave",
                                    City = "London",
                                    Province = "Ontario",
                                    Country = "Canada",
                                    PostalCode = "L1K 4M1",
                                    Phone = "905-274-2874",
                                    Email = "samjones@hotmail.com"
                                },
                                Items = new List<OrderItem>
                                {
                new OrderItem { ProductId = 9, ProductVariantId = 20, Quantity = 1, UnitPrice = 202.50m }
                                }
                            }
                        );

                        context.SaveChanges();
                    }
                }

                catch (Exception ex)
                {
                    Debug.WriteLine(ex.GetBaseException().Message);
                }
                #endregion
            }
        }
    }
}
