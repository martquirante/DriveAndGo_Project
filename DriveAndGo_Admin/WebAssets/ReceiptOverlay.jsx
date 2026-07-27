import React from 'react';

/**
 * ReceiptOverlay Component
 * 
 * Provides an Official Receipt preview overlay, compiling transaction data
 * into an 80mm thermal print format and enabling client-side PDF/print exports.
 */
export default function ReceiptOverlay({ 
  isOpen = true, 
  onClose,
  receiptData = {
    receiptNo: 'REC-2026-98103',
    rentalRef: 'REF-SUV-481923',
    customerName: 'Mart Quirante',
    vehiclePlate: 'NDG-8192',
    paymentMethod: 'GCash e-Wallet',
    totalPaid: 8500.00,
    date: '2026-07-12 21:43'
  }
}) {
  if (!isOpen) return null;

  const handlePrint = () => {
    // Elegant printing utility that opens a direct thermal layout print frame
    const printContent = document.getElementById('thermal-receipt-content');
    const windowUrl = 'about:blank';
    const uniqueName = new Date().getTime();
    const windowFeatures = 'left=50,top=50,width=400,height=600';
    const printWindow = window.open(windowUrl, uniqueName, windowFeatures);

    printWindow.document.write(`
      <html>
        <head>
          <title>Receipt Print Preview - ${receiptData.receiptNo}</title>
          <style>
            @page {
              size: 80mm auto;
              margin: 0;
            }
            body {
              font-family: 'Courier New', Courier, monospace;
              width: 74mm;
              margin: 3mm;
              padding: 0;
              background-color: #ffffff;
              color: #000000;
              font-size: 11px;
              line-height: 1.4;
            }
            .text-center { text-align: center; }
            .text-right { text-align: right; }
            .bold { font-weight: bold; }
            .divider {
              border-top: 1px dashed #000000;
              margin: 4px 0;
            }
            .header-title {
              font-size: 14px;
              font-weight: bold;
              margin-bottom: 2px;
            }
            .receipt-row {
              display: flex;
              justify-content: space-between;
              margin: 2px 0;
            }
            .total-row {
              font-size: 13px;
              margin-top: 8px;
            }
            .footer-msg {
              font-size: 9px;
              margin-top: 15px;
            }
          </style>
        </head>
        <body>
          ${printContent.innerHTML}
          <script type="text/javascript">
            window.onload = function() {
              window.print();
              setTimeout(function() { window.close(); }, 500);
            }
          </script>
        </body>
      </html>
    `);

    printWindow.document.close();
  };

  return (
    <div className="fixed inset-0 bg-[#07070e]/90 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      
      {/* Receipt Frame Modal */}
      <div className="bg-slate-950 border border-white/10 rounded-2xl w-full max-w-sm flex flex-col overflow-hidden shadow-2xl">
        
        {/* Title bar */}
        <div className="bg-slate-900/60 px-5 py-4 border-b border-white/5 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <i className="fa-solid fa-receipt text-orange-500"></i>
            <span className="text-xs font-bold text-slate-200">Official Receipt Preview</span>
          </div>
          <button 
            onClick={onClose}
            className="text-slate-500 hover:text-white transition-colors"
          >
            <i className="fa-solid fa-xmark"></i>
          </button>
        </div>

        {/* 80mm POS Thermal Layout Wrapper Container */}
        <div className="p-6 overflow-y-auto max-h-[70vh] bg-white text-slate-900 flex justify-center">
          <div 
            id="thermal-receipt-content" 
            className="w-[74mm] font-mono text-[11px] leading-relaxed p-1 bg-white text-black"
          >
            <div className="text-center">
              <div className="header-title uppercase tracking-wider font-extrabold">Drive & Go</div>
              <div className="text-[9px]">Premium Vehicle Rentals</div>
              <div className="text-[9px]">Metro Manila, Philippines</div>
              <div className="divider"></div>
              <div className="bold uppercase">Official Receipt</div>
              <div className="text-[9px] mt-0.5">{receiptData.date}</div>
            </div>

            <div className="divider"></div>

            <div className="receipt-row">
              <span>Receipt No:</span>
              <span className="bold">{receiptData.receiptNo}</span>
            </div>
            <div className="receipt-row">
              <span>Rental Ref:</span>
              <span>{receiptData.rentalRef}</span>
            </div>
            <div className="receipt-row">
              <span>Customer:</span>
              <span className="truncate max-w-[120px]">{receiptData.customerName}</span>
            </div>
            <div className="receipt-row">
              <span>Vehicle Plate:</span>
              <span className="bold">{receiptData.vehiclePlate}</span>
            </div>

            <div className="divider"></div>

            <div className="receipt-row bold">
              <span>Item / Description</span>
              <span>Total</span>
            </div>
            <div className="receipt-row">
              <span>SUV Class Base Rental</span>
              <span>₱{receiptData.totalPaid.toLocaleString()}</span>
            </div>

            <div className="divider"></div>

            <div className="receipt-row">
              <span>Payment Type:</span>
              <span>{receiptData.paymentMethod}</span>
            </div>
            <div className="receipt-row bold total-row">
              <span>Total Paid:</span>
              <span>₱{receiptData.totalPaid.toLocaleString()}</span>
            </div>

            <div className="divider"></div>

            <div className="text-center footer-msg font-semibold text-slate-500">
              Thank you for choosing Drive & Go!<br />
              Safe travels on every trip.<br />
              Support: support@driveandgo.com
            </div>
          </div>
        </div>

        {/* Output Action Panel */}
        <div className="bg-slate-900/60 p-4 border-t border-white/5 flex items-center gap-3">
          <button 
            onClick={handlePrint}
            className="flex-1 bg-orange-600 hover:bg-orange-700 text-white font-extrabold py-2.5 rounded-lg text-xs transition-colors uppercase tracking-wider flex items-center justify-center gap-1.5"
          >
            <i className="fa-solid fa-print"></i>
            <span>Print Receipt</span>
          </button>
          
          <button 
            onClick={handlePrint}
            className="bg-slate-800 hover:bg-slate-700 text-slate-300 font-bold px-4 py-2.5 rounded-lg text-xs transition-colors uppercase border border-white/5"
          >
            Download PDF
          </button>
        </div>

      </div>

    </div>
  );
}
