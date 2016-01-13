﻿using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Maps;
using System.Linq;
using Plugin.Messaging;
using Plugin.ExternalMaps;
using Plugin.ExternalMaps.Abstractions;

namespace Customers
{
    public class CustomerDetailViewModel : BaseViewModel
    {
        bool _IsNewCustomer;

        // this is just a utility service that we're using in this demo app to mitigate some limitations of the iOS simulator
        ICapabilityService _CapabilityService;

        readonly Geocoder _Geocoder;

        Map _Map;

        public CustomerDetailViewModel(Customer account = null, Map map = null)
        {
            _CapabilityService = DependencyService.Get<ICapabilityService>();

            _Geocoder = new Geocoder();

            _Map = map;

            if (account == null)
            {
                _IsNewCustomer = true;
                Account = new Customer()
                {
                    PhotoUrl = "placeholderProfileImage"
                };
            }
            else
            {
                _IsNewCustomer = false;
                Account = account;
            }

            _AddressString = Account.AddressString;

            SubscribeToSaveCustomerMessages();

            SubscribeToCustomerLocationUpdatedMessages();
        }

        public bool IsExistingCustomer { get { return !_IsNewCustomer; } }

        public bool HasEmailAddress { get { return Account != null && !String.IsNullOrWhiteSpace(Account.Email); } }

        public bool HasPhoneNumber { get { return Account != null && !String.IsNullOrWhiteSpace(Account.Phone); } }

        public bool HasAddress { get { return Account != null && !String.IsNullOrWhiteSpace(Account.AddressString); } }

        public string Title { get { return _IsNewCustomer ? "New Customer" : _Account.DisplayLastNameFirst; } }

        private string _AddressString;

        Customer _Account;

        public Customer Account
        {
            get { return _Account; }
            set
            {
                _Account = value;
                OnPropertyChanged("Account");
            }
        }

        bool _MapIsBusy = true;

        public bool MapIsBusy
        {
            get { return _MapIsBusy; }
            set
            {
                _MapIsBusy = value;
                OnPropertyChanged("MapIsBusy");
            }
        }

        Command _SaveCustomerCommand;

        /// <summary>
        /// Command to save customer
        /// </summary>
        public Command SaveCustomerCommand
        {
            get
            {
                return _SaveCustomerCommand ??
                (_SaveCustomerCommand = new Command(async () =>
                        await ExecuteSaveCustomerCommand()));
            }
        }

        async Task ExecuteSaveCustomerCommand()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            if (String.IsNullOrWhiteSpace(Account.LastName) || String.IsNullOrWhiteSpace(Account.FirstName))
            {
                // display an alert, letting the user know that we require a first and last name in order to save a new customer
                await Page.DisplayAlert(
                    title: "Invalid name!", 
                    message: "A customer must have both a first and last name.",
                    cancel: "OK");
            }
            else if (!RequiredAddressFieldCombinationIsFilled)
            {
                // display an alert, letting the user know that we require a first and last name in order to save a new customer
                await Page.DisplayAlert(
                    title: "Invalid address!", 
                    message: "You must enter either a street, city, and state combination, or a postal code.",
                    cancel: "OK");
            }
            else
            {
                // send a message via MessagingCenter that we want the given customer to be saved
                MessagingCenter.Send(this.Account, "SaveCustomer");

                // perform a pop in order to navigate back to the customer list
                await Navigation.PopAsync();
            }

            IsBusy = false;
        }

        bool RequiredAddressFieldCombinationIsFilled
        {
            get 
            {
                if (Account.AddressString.IsNullOrWhiteSpace())
                {
                    return true;
                }
                else if (!Account.Street.IsNullOrWhiteSpace() && (!Account.City.IsNullOrWhiteSpace() && !Account.State.IsNullOrWhiteSpace()))
                {
                    return true;
                }
                else if (!Account.PostalCode.IsNullOrWhiteSpace() && (Account.Street.IsNullOrWhiteSpace() || Account.City.IsNullOrWhiteSpace() || Account.State.IsNullOrWhiteSpace()))
                {
                    return true;
                }

                return false;
            }
        }

        Command _EditCustomerCommand;

        /// <summary>
        /// Command to edit customer
        /// </summary>
        public Command EditCustomerCommand
        {
            get
            {
                return _EditCustomerCommand ??
                (_EditCustomerCommand = new Command(async () =>
                        await ExecuteEditCustomerCommand()));
            }
        }

        async Task ExecuteEditCustomerCommand()
        {
            var editPage = new CustomerEditPage();

            var viewmodel = this;

            viewmodel.Page = editPage;

            editPage.BindingContext = viewmodel;

            await Navigation.PushAsync(editPage);
        }


        Command _DeleteCustomerCommand;

        /// <summary>
        /// Command to delete customer
        /// </summary>
        public Command DeleteCustomerCommand
        {
            get
            {
                return _DeleteCustomerCommand ??
                (_DeleteCustomerCommand = new Command(async () =>
                        await ExecuteDeleteCustomerCommand()));
            }
        }

        async Task ExecuteDeleteCustomerCommand()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            // display an alert and get the result of the user's decision
            var confirmDelete = 
                await Page.DisplayAlert(
                    title: String.Format("Delete {0}?", Account.DisplayName), 
                    message: null, 
                    accept: "Delete", 
                    cancel: "Cancel");

            if (confirmDelete)
            {
                // send a message via MessagingCenter that we want the given customer to be deleted
                MessagingCenter.Send(this.Account, "DeleteCustomer");

                // Performs two pops, not one. We want to navigate back to the list, not the detail screen.
                await Navigation.PopAsync(false); // passing false here to avoid two animations

                await Navigation.PopAsync();
            }

            IsBusy = false;
        }

        Command _DialNumberCommand;

        /// <summary>
        /// Command to dial customer phone number
        /// </summary>
        public Command DialNumberCommand
        {
            get
            {
                return _DialNumberCommand ??
                (_DialNumberCommand = new Command(async () =>
                        await ExecuteDialNumberCommand()));
            }
        }

        async Task ExecuteDialNumberCommand()
        {
            if (String.IsNullOrWhiteSpace(Account.Phone))
                return;

            if (_CapabilityService.CanMakeCalls)
            {
                var phoneCallTask = MessagingPlugin.PhoneDialer;
                if (phoneCallTask.CanMakePhoneCall)
                    phoneCallTask.MakePhoneCall(Account.Phone.SanitizePhoneNumber());
            }
            else
            {
                await Page.DisplayAlert(
                    title: "Simulator Not Supported", 
                    message: "Phone calls are not supported in the iOS simulator.",
                    cancel: "OK");
            }
        }

        Command _MessageNumberCommand;

        /// <summary>
        /// Command to message customer phone number
        /// </summary>
        public Command MessageNumberCommand
        {
            get
            {
                return _MessageNumberCommand ??
                (_MessageNumberCommand = new Command(async () =>
                        await ExecuteMessageNumberCommand()));
            }
        }

        async Task ExecuteMessageNumberCommand()
        {
            if (String.IsNullOrWhiteSpace(Account.Phone))
                return;

            if (_CapabilityService.CanSendMessages)
            {
                var messageTask = MessagingPlugin.SmsMessenger;
                if (messageTask.CanSendSms)
                    messageTask.SendSms(Account.Phone.SanitizePhoneNumber());
            }
            else
            {
                await Page.DisplayAlert(
                    title: "Simulator Not Supported", 
                    message: "Messaging is not supported in the iOS simulator.",
                    cancel: "OK");
            }
        }

        Command _EmailCommand;

        /// <summary>
        /// Command to email customer
        /// </summary>
        public Command EmailCommand
        {
            get
            {
                return _EmailCommand ??
                (_EmailCommand = new Command(async () =>
                        await ExecuteEmailCommandCommand()));
            }
        }

        async Task ExecuteEmailCommandCommand()
        {
            if (String.IsNullOrWhiteSpace(Account.Email))
                return;

            if (_CapabilityService.CanSendEmail)
            {
                var emailTask = MessagingPlugin.EmailMessenger;
                if (emailTask.CanSendEmail)
                    emailTask.SendEmail(Account.Email);
            }
            else
            {
                await Page.DisplayAlert(
                    title: "Simulator Not Supported", 
                    message: "Email composition is not supported in the iOS simulator.",
                    cancel: "OK");
            }
        }

        Command _GetDirectionsCommand;

        public Command GetDirectionsCommand
        {
            get
            {
                return _GetDirectionsCommand ??
                (_GetDirectionsCommand = new Command(async() => 
                        await ExecuteGetDirectionsCommand()));
            }
        }

        async Task ExecuteGetDirectionsCommand()
        {
            var position = await GetPosition();

            var pin = new Pin() { Position = position };

            CrossExternalMaps.Current.NavigateTo(pin.Label, pin.Position.Latitude, pin.Position.Longitude, NavigationType.Driving);
        }

        public async Task SetupMap()
        {
            _Map.IsVisible = false;

            if (HasAddress)
            {

                // set to a default posiion
                var position = new Position(0, 0);

                try
                {
                    position = await GetPosition();
                }
                catch
                {
                    await DisplayGeocodingError();

                    return;
                }

                // if lat and lon are both 0, then it's assumed that position acquisition failed
                if (position.Latitude == 0 && position.Longitude == 0)
                {
                    await DisplayGeocodingError();

                    return;
                }
                else
                {
                    var pin = new Pin()
                    { 
                        Type = PinType.Place, 
                        Position = position,
                        Label = Account.DisplayName, 
                        Address = Account.AddressString 
                    };

                    _Map.Pins.Clear();

                    _Map.Pins.Add(pin);

                    _Map.MoveToRegion(MapSpan.FromCenterAndRadius(pin.Position, Distance.FromMiles(10)));

                    _Map.IsVisible = true;
                }
            }
        }

        async Task DisplayGeocodingError()
        {
            await Page.DisplayAlert(
                "Geocoding Error", 
                "Please make sure the address is valid.",
                "OK");

            MapIsBusy = false;
        }

        public async Task<Position> GetPosition()
        {
            MapIsBusy = true && HasAddress;

            Position p;

            p = (await _Geocoder.GetPositionsForAddressAsync(Account.AddressString)).FirstOrDefault();

            // The Android geocoder (the underlying implementation in Android itself) fails with some addresses unless they're rounded to the hundreds.
            // So, this deals with that edge case.
            if (p.Latitude == 0 && p.Longitude == 0 && AddressBeginsWithNumber(Account.AddressString))
            {
                var roundedAddress = GetAddressWithRoundedStreetNumber(Account.AddressString);

                p = (await _Geocoder.GetPositionsForAddressAsync(roundedAddress)).FirstOrDefault();
            }

            return p;
        }

        void SubscribeToSaveCustomerMessages()
        {
            // This subscribes to the "SaveCustomer" message
            MessagingCenter.Subscribe<Customer>(this, "SaveCustomer", (customer) =>
                {
                    Account = customer;

                    OnPropertyChanged("Account");

                    // address has been updated, so we should update the map
                    if (Account.AddressString != _AddressString)
                    {
                        MessagingCenter.Send(this, "CustomerLocationUpdated");

                        _AddressString = Account.AddressString;
                    }
                });
        }

        void SubscribeToCustomerLocationUpdatedMessages()
        {
            // update the map when receiving the CustomerLocationUpdated message
            MessagingCenter.Subscribe<CustomerDetailViewModel>(this, "CustomerLocationUpdated", async (sender) =>
                {
                    OnPropertyChanged("HasAddress");

                    await SetupMap();
                });
        }

        static bool AddressBeginsWithNumber(string address)
        {
            return !String.IsNullOrWhiteSpace(address) && Char.IsDigit(address.ToCharArray().First());
        }

        static string GetAddressWithRoundedStreetNumber(string address)
        {
            var endingIndex = GetEndingIndexOfNumericPortionOfAddress(address);

            if (endingIndex == 0)
                return address;

            int originalNumber = 0;
            int roundedNumber = 0;

            Int32.TryParse(address.Substring(0, endingIndex + 1), out originalNumber);

            if (originalNumber == 0)
                return address;

            roundedNumber = originalNumber.RoundToLowestHundreds();

            return address.Replace(originalNumber.ToString(), roundedNumber.ToString());
        }

        static int GetEndingIndexOfNumericPortionOfAddress(string address)
        {
            int endingIndex = 0;

            for (int i = 0; i < address.Length; i++)
            {
                if (Char.IsDigit(address[i]))
                    endingIndex = i;
                else
                    break;
            }

            return endingIndex;
        }
    }
}

