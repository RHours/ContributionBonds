# Contribution Bonds
Contribution Bonds are digital documents which represent the parameters of an agreement between a revenue generating organization and an entity which has contributed something of value. This specification is not finalized.

# General Format
Following is an example of a contribution bond in JSON format.

```javascript
{
    "@context": "https://github.com/RHours/ContributionBonds",
    "id": "BOND_DID",
    "terms": "TERMS_HASH_STRING",
    "company": "COMPANY_DID",
    "contributor": "CONTRIBUTOR_DID",
    "created": "UTC creation date, format is yyyy-MM-ddThh:mm:ss",
    "amount": 100.00,
    "interest-rate": 0.25,
    "max": 1000.00,
    "unit": "USD",
    "payments": 
        [
            {
                "date": "UTC payment date, format is yyyy-MM-ddThh:mm:ss",
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


# RHours DID Document Elements
Element             | Description
-------             | -----------
@context            | "https://www.w3.org/2019/did/v1"
id                  | The DID this document refers to.
authentication      | An array of verification methods.


## RHours DID Document Verification Method Elements
Element             | Description
-------             | -----------
id                  | The ID of this verification method.
type                | One of the linked data cryptographic suites at https://w3c-ccg.github.io/ld-cryptosuite-registry/
controller          | The DID of the subject who controls the key.
publicKeyPem        | The PEM of the public key, format is https://tools.ietf.org/html/rfc7468


## Signature and Validation using RsaVerificationKey2018
https://w3c-dvcg.github.io/ld-signatures/
https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.rsapkcs1signatureformatter?view=netframework-4.8


"proof": [{
    "type": "RsaSignature2018",
    "creator": "https://example.com/i/pat/keys/5",
    "created": "2017-09-23T20:21:34Z",
    "nonce": "2bbgh3dgjg2302d-d2b3gi423d42",
    "proofValue": "eyJ0eXAiOiJK...gFWFOEjXk"
  }, {
    "type": "RsaSignature2018",
    "creator": "https://example.com/i/kelly/keys/7f3j",
    "created": "2017-09-23T20:24:12Z",
    "nonce": "83jj4hd62j49gk38",
    "proofValue": "eyiOiJJ0eXAK...EjXkgFWFO"
  }]

  ## Canonicalization of JSON data
  It's possible for mutlitple JSON documents to have same data but different sequence of bytes. For example, adding extra whitespace to one document would change the sequence of bytes but still represent the same data. This causes a problem when cryptographically signing JSON documents. The signing occurs on bytes not the meaning of the data. 

  To ensure that two documents which have the same meaningful data result in the same signature, a process called canonicalization is used to transform each document into a standardized form which are then signed.

  Additionally, its convienient to embed the signature of a document within the document itself. However, this implies that the signature itself cannot be included within the bytes signed. You would need to know the signature to include it in the bytes of the signature.

  Within the JSON-LD orbit of specifications, there is a specification for signing JSON-LD data. But, for the life of me, I can't make heads or tails of what the algoritm really is. Much of the JSON-LD work came from previous work on RDF so the canonicalization contains references to RDF which seem unaplicable to JSON. So, I've desgned my own scheme as below.

  * The signature will operate over the bytes which represent a UTF-8 encoding of the text of a JSON document.
  * All whitespace will be removed (which is not part of a string)
  * String characters will be normalized using these rules
  ** Characters escapes will be written as the two character escape of '\' + one of ('"' '\' 'b' 'f' 'n' 'r' 't')
  ** The forward slash character '/' will be written as one character
  ** All other valid JSON characters are written as one character
  * Numbers with no decimal component will be written as integers even if internally modeled as floats.
  * The lable names of object properties are sorted based on their unicode value. Duplicates are not allowed in JSON so there's no issue of picking between duplicates.
  * The signing process will ignore a final object property called "proof". The "proof" label must be the last one in the root object. Only objects can be signed. There's no support for signature chains in this sceme but proof sets are supported.
  * The signing process will update an existing "proof" label or create a new one. A "proof" property may contain either a single object, or an array of objects. The "proof" objects structure is defined  at https://w3c-dvcg.github.io/ld-signatures.
  * The signature bytes are prepended with a nonce. The nonce is given in the "proof" object.

# Command Line User Interface

* Initialize a project
** Creates the directory structure to store the public portion of identites and bonds.
** Creates a company identity
** Private information is written to the screen
** If these things exist already initialization succeeds
* Create Contributor Identity
** Generates the DID string and DID document for a contributor identity
** Private information is written to the screen
* Create a Bond
** The data items needed to create a bond are given
** The bond will be unsigned and will have no payments recorded.
** Bonds are identified by a DID
* Sign Bond
** Give DID string of contributor or company
** Give path to private key
** Updates the bond with the signature
* Record Payment
** Given the DID of a bond
** Given an amount of the payment
** Updates the bond with the payment information, bond will need to be signed again by both company and contributor.
* Verify Bond
** Verifies the signatures of a bond
** Verifies schema compliance


## Command Syntax
General form
```
cb command sub-command --param=value --param=value ...

    command     sub-command     parameters      value
    -------     -----------     ----------      -----
    company     init[ialize]    --root          Path to root folder of the company project.
                                                By the convention, the same as the Git root.
                                                Optional - the current directory is used
                                                           if it contains a .git folder.

        Action: This command creates the folders used to hold the contributors and bonds. It
                also creates an indentity which represents the entity which owns the project, called
                the 'company'.
                If the folders and company identity already exist, the command succeeds.

        Output: The private PEM information for the company identity. This
                value must be saved in a secure location and kept private. Do not lose this information, 
                without it the ownership of the company can not be proved.

    command     sub-command     parameters      value
    -------     -----------     ----------      -----
    contributor create          --root          ...

        Action: Creates DID string, DID Document, and private key for a contributor. The public 
                information is written to the correct folder in the project to be committed.

        Output: The private PEM information for the contributor identity. This
                value must be saved to a secure location and kept private. Do not lose this information,
                without it the ownership of the contributor identity cannot be proved.

    command     sub-command     parameters      value
    -------     -----------     ----------      -----
    bond        create          --terms         A path to a file containing the written terms of the bond agreement.
                                --contributor   DID string of the contributor the bond is being issued to.
                                --amount        The dollar amount of the bond.
                                --rate          The interest rate, expressed as a decimal, e.g., 0.25 is 25%.
                                --max           The maximum total payments for the bond.

        Action: Creates a new bond file with the information provided. The bond is not signed and there are
                no payments recorded.

        Output: Success or failure messages.

    bond        sign            --id            The DID string for the bond.
                                --signatory     The DID string of the identity signing the bond. This
                                                needs to be either the contributor or the company.

        Action: The command will wait for a private key PEM string to be entered into the standard input. Then
                it will sign and update the bond file with the new signature.

        Output: Success or failure messages.

    bond        payment         --id            The DID string for the bond.
                                --amount        The amount of payment to apply to the bond. This number may be
                                                adjusted downward to avoid overpayment.

        Action: Calculates the interest amount since the last payment and updates the bond with 
                a new payment record. A new balance is calculated and stored with the new payment.

        Ouput:  A summary of the successful payment or error messages.

    bond       verify           --id            The DID string for the bond.

        Action: The bond signatures are verified.

        Output: The latest dates of the company and contributor signatures, or error messages.
```

