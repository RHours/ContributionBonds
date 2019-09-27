# Contribution Bonds
Contribution Bonds are digital documents which represent the parameters of an agreement between a revenue generating organization and an entity which has contributed something of value. This specification is not finalized.

# General Format
Following is an example of a contribution bond in JSON format.

```javascript
{
    "terms": "TERMS_HASH_STRING",
    "company": "COMPANY_IDENTIFIER",
    "contributor": "CONTRIBUTOR_IDENTIFIER",
    "date": "yyyy-mm-dd",
    "amount": 100.00,
    "interest-rate": 0.25,
    "max": 1000.00,
    "unit": "USD",
    "payments": 
        [
            {
                "date": "yyyy-mm-dd",
                "interest": 16.52,
                "amount": 40.00,
                "balance": 76.52
            }
        ]
}
```

## Bond Elements
Element             | Description
-------             | -----------
terms               | The hash of a description of the terms of the bond.
company             | A decentralized identifier (DID) of the revenue generating organization issuing the bond.
contributor         | A decentralized identifier (DID) of the contributor.
date                | The date the bond was issued and from which interest begins accruing.
amount              | The initial amount of the bond.
interest-rate       | The interest rate. The method of calculation is stated in the version terms.
max                 | An upper bound for the total payments to be made to the contributor, regardless of interest calculation.
unit                | The monetary unit.
payments            | A list of payments made by the company to the contributor.

## Bond Payments Elements
Element             | Description
-------             | -----------
date                | The date the payment was made.
interest            | The amount of interest accrued since the outstanding balance for the current interest period.
amount              | The amount of the payment.
balance             | The remaining balance of the bond (previous balance + interest - payment amount)


# Bond Terms
A detailed description of the terms of the agreement is specified in a separate document from the bond. This term description file defines the nature of the agreement, for example, how interest is calculated and what algorithms are used for signing the bond.

# Company and Contributor Identifiers
Identity is provided through cryptographic signatures. Each party can generate an identity independently by generating a public/private key pair. Each party is responsible for keeping the private portion of the key hidden from any other entity. Anyone who has access to the private key can sign on behalf of the party associated with the key pair.

## Decentralized Identifiers (DID)
Contribution Bonds use Decentralized Identifiers which are specified by a W3C Community Group at https://w3c-ccg.github.io/did-spec/. This tool can be used to create and manage Decentralized Identifiers with the DID method name of "rhours". Any DID which can resolve to provide a public key is valid for a contribution bond though only the "rhours" method name is supported by this tool.

# Cryptographic Signatures
Both the company and contributor must sign a contribution bond for it to be valid. The signature algorithms are specified in the terms version document.

# Operations on a Contribution Bond
Payments or other operations create a new version of the bond. The cryptographically verifiable version of a bond document with a date greater than another bond document supersedes any bond documents with a prior date.

## Payment Operations
A payment operation adds an element to the payments array of the bond.

## Voiding Operations
A bond can be voided by adding a { "void-date": "VOID_DATE" } element to the payments array. All payments before the void date are valid but no future payments are required by the organization.



